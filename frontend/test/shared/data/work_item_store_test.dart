import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

import '../fakes/work_items.dart';

WorkItemStore _storeWith(List<WorkItem> items) =>
    WorkItemStore()..putAll(items, asOf: 0);

WorkItemBlocker _blocker(String id, {required bool conflicts}) =>
    WorkItemBlocker(
      id: id,
      number: 1,
      title: id,
      conflicts: conflicts,
    );

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
        reload: (_) => throw StateError('No conflict expected'),
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

  test('an item loaded again after removal is not mistaken for a newer '
      'saved copy', () async {
    final store = _storeWith([workItem('a')]);
    await store.write(
      'a',
      send: (_) async => workItem('a', version: 2),
      reload: (_) => throw StateError('No conflict expected'),
    );
    store
      ..remove('a')
      ..putAll([workItem('a', title: 'Restored', version: 7)], asOf: 0);

    expect(store['a']?.title, 'Restored');
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
          reload: (_) => throw StateError('No conflict expected'),
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
          reload: (_) => throw StateError('No conflict expected'),
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
        reload: (_) => throw StateError('No conflict expected'),
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
        reload: (_) => throw StateError('No conflict expected'),
      );
      await pumpEventQueue();
      expect(sentVersions, {'status': 1}, reason: 'tags wait for status');

      firstResponse.complete(workItem('a', statusId: 'done', version: 2));
      await first;
      expect(
        store['a']?.tags,
        ['urgent'],
        reason: 'the unsaved tag edit still shows over the saved status',
      );

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
          reload: (_) => throw StateError('No conflict expected'),
        ),
      );
      await store.write(
        'b',
        send: (_) async {
          bSent = true;
          return workItem('b', version: 2);
        },
        reload: (_) => throw StateError('No conflict expected'),
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
          reload: (_) => throw StateError('No conflict expected'),
        );

        final write = store.write(
          'a',
          optimistic: (item) => item.withStatus('done'),
          send: (_) async => throw conflict,
          reload: (_) async =>
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
        reload: (_) async => throw const ApiException('Offline'),
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

    test('fails the queued writes too when the reload fails', () async {
      final store = _storeWith([workItem('a')]);
      final firstResponse = Completer<WorkItem>();

      final first = store.write(
        'a',
        send: (_) => firstResponse.future,
        reload: (_) async => throw const ApiException('Offline'),
      );
      final queued = store.write(
        'a',
        send: (_) async => workItem('a', version: 2),
        reload: (_) async => throw const ApiException('Offline'),
      );
      firstResponse.completeError(conflict);

      await expectLater(first, throwsA(isA<WorkItemConflict>()));
      await expectLater(queued, throwsA(isA<WorkItemConflict>()));
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
        reload: (_) async => workItem('a', version: 9),
      );
      final queued = store.write(
        'a',
        optimistic: (item) => item.tagged(const ['urgent']),
        send: (_) async {
          queuedSent = true;
          return workItem('a', version: 10);
        },
        reload: (_) async => workItem('a', version: 9),
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
          reload: (_) async => workItem('a', version: 9),
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
        reload: (_) => throw StateError('No conflict expected'),
      );

      expect(sentVersion, 9);
    });
  });

  group('a 409 the item has not changed since', () {
    // The server also answers 409 when the change itself breaks a rule,
    // such as a parent that would make a cycle.
    const rejection = ApiException('It would create a cycle.', statusCode: 409);

    test('is rethrown as it is, not as a conflict', () async {
      final store = _storeWith([workItem('a', version: 3)]);

      final write = store.write(
        'a',
        optimistic: (item) => item.movedToParent('b'),
        send: (_) async => throw rejection,
        reload: (_) async => workItem('a', version: 3),
      );

      await expectLater(
        write,
        throwsA(
          isA<ApiException>()
              .having((e) => e, 'error', isNot(isA<WorkItemConflict>()))
              .having((e) => e.message, 'message', rejection.message),
        ),
      );
      expect(store['a']?.parentId, isNull);
    });

    test('does not reject the writes queued behind it', () async {
      final store = _storeWith([workItem('a', version: 3)]);
      final firstResponse = Completer<WorkItem>();

      final first = store.write(
        'a',
        send: (_) => firstResponse.future,
        reload: (_) async => workItem('a', version: 3),
      );
      int? queuedVersion;
      final queued = store.write(
        'a',
        send: (version) async {
          queuedVersion = version;
          return workItem('a', version: 4);
        },
        reload: (_) => throw StateError('No conflict expected'),
      );
      firstResponse.completeError(rejection);

      await expectLater(first, throwsA(isA<ApiException>()));
      await queued;
      expect(queuedVersion, 3);
    });
  });

  // A blocked item's conflict flag depends on its blocker's dates, but
  // moving those dates doesn't bump the blocked item's version.
  group('a save that moves the dates', () {
    WorkItemStore drywallBlockingPaint() => _storeWith([
      workItem('drywall', endDate: DateTime(2026, 11, 20)),
      workItem(
        'paint',
        startDate: DateTime(2026, 11, 18),
        blockedBy: [_blocker('drywall', conflicts: true)],
      ),
      workItem('fence'),
    ]);

    test('reloads the items it blocks', () async {
      final store = drywallBlockingPaint();
      final reloaded = <String>[];

      await store.write(
        'drywall',
        send: (_) async =>
            workItem('drywall', endDate: DateTime(2026, 11, 17), version: 2),
        reload: (id) async {
          reloaded.add(id);
          return workItem(
            id,
            startDate: DateTime(2026, 11, 18),
            blockedBy: [_blocker('drywall', conflicts: false)],
            version: 1,
          );
        },
      );
      await pumpEventQueue();

      expect(reloaded, ['paint']);
      expect(store['paint']?.blockedBy.single.conflicts, isFalse);
    });

    test('reloads them when a date is cleared too', () async {
      final store = drywallBlockingPaint();
      final reloaded = <String>[];

      await store.write(
        'drywall',
        send: (_) async => workItem('drywall', version: 2),
        reload: (id) async {
          reloaded.add(id);
          return workItem(id);
        },
      );
      await pumpEventQueue();

      expect(reloaded, ['paint']);
    });

    test('keeps the blocked copy when its reload fails', () async {
      final store = drywallBlockingPaint();

      final saved = await store.write(
        'drywall',
        send: (_) async =>
            workItem('drywall', endDate: DateTime(2026, 11, 17), version: 2),
        reload: (_) async => throw const ApiException('Offline'),
      );
      await pumpEventQueue();

      expect(saved.endDate, DateTime(2026, 11, 17));
      expect(store['paint']?.blockedBy.single.conflicts, isTrue);
    });

    test('does not bring back a blocked item removed meanwhile', () async {
      final store = drywallBlockingPaint();
      final reload = Completer<WorkItem>();

      await store.write(
        'drywall',
        send: (_) async =>
            workItem('drywall', endDate: DateTime(2026, 11, 17), version: 2),
        reload: (_) => reload.future,
      );
      store.remove('paint');
      reload.complete(workItem('paint', version: 2));
      await pumpEventQueue();

      expect(store['paint'], isNull);
    });
  });

  test('a save that leaves the dates alone reloads nothing', () async {
    final store = _storeWith([
      workItem('drywall', endDate: DateTime(2026, 11, 20)),
      workItem('paint', blockedBy: [_blocker('drywall', conflicts: true)]),
    ]);

    await store.write(
      'drywall',
      send: (_) async => workItem(
        'drywall',
        statusId: 'done',
        endDate: DateTime(2026, 11, 20),
        version: 2,
      ),
      reload: (_) => throw StateError('No reload expected'),
    );
    await pumpEventQueue();

    expect(store['paint']?.blockedBy.single.conflicts, isTrue);
  });
}
