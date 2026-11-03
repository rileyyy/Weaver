import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/work_item.dart';

import '../fakes/work_items.dart';

WorkItemStore _storeWith(List<WorkItem> items) =>
    WorkItemStore()..putAll(items, asOf: 0);

void main() {
  test('holds one copy of each loaded item, the latest load winning', () {
    final store = WorkItemStore()
      ..putAll([workItem('a', title: 'Old'), workItem('b')], asOf: 0)
      ..putAll([workItem('a', title: 'New', version: 2)], asOf: 0);

    expect(store['a']?.title, 'New');
    expect(store['a']?.version, 2);
    expect(store['b'], isNotNull);
    expect(store['missing'], isNull);
  });

  test(
    'a load that started before a write finished keeps the written copy',
    () async {
      final store = _storeWith([workItem('a'), workItem('b')]);
      final loadStartedAt = store.revision;

      await store.write(
        'a',
        send: (_) async => workItem('a', statusId: 'done', version: 2),
        reload: () => throw StateError('No conflict expected'),
      );
      store.putAll([
        workItem('a', statusId: 'todo', version: 1),
        workItem('b', title: 'Renamed', version: 5),
      ], asOf: loadStartedAt);

      expect(store['a']?.statusId, 'done');
      expect(store['a']?.version, 2);
      expect(store['b']?.title, 'Renamed');
    },
  );

  test('a removed item is no longer held, and listeners hear of it', () {
    final store = _storeWith([workItem('a'), workItem('b')]);
    var notified = false;
    store
      ..addListener(() => notified = true)
      ..remove('a');

    expect(store['a'], isNull);
    expect(store['b'], isNotNull);
    expect(notified, isTrue);
  });

  group('write', () {
    test(
      'sends the stored version and keeps the item the server returns',
      () async {
        final store = _storeWith([workItem('a', version: 4)]);
        int? sentVersion;

        final saved = await store.write(
          'a',
          send: (expectedVersion) async {
            sentVersion = expectedVersion;
            return workItem('a', statusId: 'done', version: 5);
          },
          reload: () => throw StateError('No conflict expected'),
        );

        expect(sentVersion, 4);
        expect(saved.version, 5);
        expect(store['a']?.statusId, 'done');
        expect(store['a']?.version, 5);
      },
    );

    test(
      'shows the optimistic change at once and drops it on failure',
      () async {
        final store = _storeWith([workItem('a')]);
        final response = Completer<WorkItem>();

        final write = store.write(
          'a',
          optimistic: (item) => item.withStatus('done'),
          send: (_) => response.future,
          reload: () => throw StateError('No conflict expected'),
        );
        expect(store['a']?.statusId, 'done');

        response.completeError(
          const ApiException('Server error', statusCode: 500),
        );
        await expectLater(write, throwsA(isA<ApiException>()));
        expect(store['a']?.statusId, 'todo');
      },
    );

    test('overlapping writes to one item go one at a time, each with the '
        'version the previous one returned', () async {
      final store = _storeWith([workItem('a', version: 1)]);
      final firstResponse = Completer<WorkItem>();
      final sentVersions = <String, int>{};

      final first = store.write(
        'a',
        optimistic: (item) => item.withStatus('done'),
        send: (version) {
          sentVersions['status'] = version;
          return firstResponse.future;
        },
        reload: () => throw StateError('No conflict expected'),
      );
      final second = store.write(
        'a',
        optimistic: (item) => item.tagged(const ['urgent']),
        send: (version) async {
          sentVersions['tags'] = version;
          return workItem(
            'a',
            statusId: 'done',
            tags: const ['urgent'],
            version: 3,
          );
        },
        reload: () => throw StateError('No conflict expected'),
      );
      await pumpEventQueue();
      expect(sentVersions, {'status': 1}, reason: 'tags wait for status');

      firstResponse.complete(workItem('a', statusId: 'done', version: 2));
      await first;
      expect(store['a']?.tags, [
        'urgent',
      ], reason: 'the unsaved tag edit still shows over the saved status');

      await second;
      expect(sentVersions, {'status': 1, 'tags': 2});
      expect(store['a']?.version, 3);
    });

    test('writes to different items do not wait for each other', () async {
      final store = _storeWith([workItem('a'), workItem('b')]);
      final slow = Completer<WorkItem>();
      var bSent = false;

      unawaited(
        store.write(
          'a',
          send: (_) => slow.future,
          reload: () => throw StateError('No conflict expected'),
        ),
      );
      await store.write(
        'b',
        send: (_) async {
          bSent = true;
          return workItem('b', version: 2);
        },
        reload: () => throw StateError('No conflict expected'),
      );

      expect(bSent, isTrue);
      slow.complete(workItem('a', version: 2));
    });
  });

  group('a conflict', () {
    const conflict = ApiException('Changed by someone else', statusCode: 409);

    test(
      'reloads just that item and reports it, leaving other items alone',
      () async {
        final store = _storeWith([workItem('a'), workItem('b')]);
        final otherResponse = Completer<WorkItem>();
        final otherWrite = store.write(
          'b',
          optimistic: (item) => item.withStatus('done'),
          send: (_) => otherResponse.future,
          reload: () => throw StateError('No conflict expected'),
        );

        final write = store.write(
          'a',
          optimistic: (item) => item.withStatus('done'),
          send: (_) async => throw conflict,
          reload: () async =>
              workItem('a', title: 'Their title', statusId: 'todo', version: 9),
        );

        await expectLater(
          write,
          throwsA(
            isA<WorkItemConflict>().having(
              (e) => e.latest?.title,
              'latest title',
              'Their title',
            ),
          ),
        );
        expect(store['a']?.title, 'Their title');
        expect(store['a']?.statusId, 'todo');
        expect(store['a']?.version, 9);
        expect(store['b']?.statusId, 'done', reason: 'still being saved');

        otherResponse.complete(workItem('b', statusId: 'done', version: 2));
        await otherWrite;
      },
    );

    test('keeps the item as it was when the reload fails too', () async {
      final store = _storeWith([workItem('a', version: 3)]);

      final write = store.write(
        'a',
        optimistic: (item) => item.withStatus('done'),
        send: (_) async => throw conflict,
        reload: () async => throw const ApiException('Offline'),
      );

      await expectLater(
        write,
        throwsA(
          isA<WorkItemConflict>().having((e) => e.latest, 'latest', isNull),
        ),
      );
      expect(store['a']?.statusId, 'todo');
      expect(store['a']?.version, 3);
    });

    test('rejects the writes queued behind it unsent, since they were made '
        'against the stale copy', () async {
      final store = _storeWith([workItem('a')]);
      final firstResponse = Completer<WorkItem>();
      var queuedSent = false;

      final first = store.write(
        'a',
        optimistic: (item) => item.withStatus('done'),
        send: (_) => firstResponse.future,
        reload: () async => workItem('a', version: 9),
      );
      final queued = store.write(
        'a',
        optimistic: (item) => item.tagged(const ['urgent']),
        send: (_) async {
          queuedSent = true;
          return workItem('a', version: 10);
        },
        reload: () async => workItem('a', version: 9),
      );
      firstResponse.completeError(conflict);

      await expectLater(first, throwsA(isA<WorkItemConflict>()));
      await expectLater(queued, throwsA(isA<WorkItemConflict>()));
      expect(queuedSent, isFalse);
      expect(store['a']?.tags, isEmpty);
      expect(store['a']?.version, 9);
    });

    test('does not hold up writes started after it', () async {
      final store = _storeWith([workItem('a')]);
      await expectLater(
        store.write(
          'a',
          send: (_) async => throw conflict,
          reload: () async => workItem('a', version: 9),
        ),
        throwsA(isA<WorkItemConflict>()),
      );

      int? sentVersion;
      await store.write(
        'a',
        send: (version) async {
          sentVersion = version;
          return workItem('a', version: 10);
        },
        reload: () => throw StateError('No conflict expected'),
      );

      expect(sentVersion, 9);
    });
  });
}
