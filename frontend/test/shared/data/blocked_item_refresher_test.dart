import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/data/blocked_item_refresher.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

import '../fakes/work_items.dart';

WorkItemBlocker _blocker(String id, {required bool conflicts}) =>
    WorkItemBlocker(id: id, number: 1, title: id, conflicts: conflicts);

WorkItemStore _drywallBlockingPaint() => WorkItemStore()
  ..putAll([
    workItem('drywall', endDate: DateTime(2026, 11, 20)),
    workItem(
      'paint',
      startDate: DateTime(2026, 11, 18),
      blockedBy: [_blocker('drywall', conflicts: true)],
    ),
    workItem('fence'),
  ], asOf: 0);

WorkItem _paintNoLongerConflicting(String id) => workItem(
  id,
  startDate: DateTime(2026, 11, 18),
  blockedBy: [_blocker('drywall', conflicts: false)],
);

void main() {
  group('changesConflicts', () {
    final base = workItem(
      'a',
      startDate: DateTime(2026, 11, 1),
      endDate: DateTime(2026, 11, 5),
    );

    for (final (name, after, expected) in [
      ('a moved start', workItem('a', endDate: DateTime(2026, 11, 5)), true),
      (
        'a moved end',
        workItem(
          'a',
          startDate: DateTime(2026, 11, 1),
          endDate: DateTime(2026, 11, 6),
        ),
        true,
      ),
      (
        'entering Done',
        workItem(
          'a',
          startDate: DateTime(2026, 11, 1),
          endDate: DateTime(2026, 11, 5),
          completionDate: DateTime(2026, 11, 4),
        ),
        true,
      ),
      (
        'another field only',
        workItem(
          'a',
          title: 'Renamed',
          startDate: DateTime(2026, 11, 1),
          endDate: DateTime(2026, 11, 5),
        ),
        false,
      ),
    ]) {
      test('is $expected for $name', () {
        expect(BlockedItemRefresher.changesConflicts(base, after), expected);
      });
    }

    test('is true for leaving Done', () {
      final done = workItem('a', completionDate: DateTime(2026, 11, 4));

      expect(
        BlockedItemRefresher.changesConflicts(done, workItem('a')),
        isTrue,
      );
    });

    test('is false for moving between Done statuses', () {
      final done = workItem('a', completionDate: DateTime(2026, 11, 4));
      final otherDone = workItem(
        'a',
        statusId: 'shipped',
        completionDate: DateTime(2026, 11, 4),
      );

      expect(BlockedItemRefresher.changesConflicts(done, otherDone), isFalse);
    });
  });

  group('refreshItemsBlockedBy', () {
    test('reloads only the held items the blocker blocks', () async {
      final store = _drywallBlockingPaint();
      final reloaded = <String>[];

      await BlockedItemRefresher(store, (id) async {
        reloaded.add(id);
        return _paintNoLongerConflicting(id);
      }).refreshItemsBlockedBy('drywall');

      expect(reloaded, ['paint']);
      expect(store['paint']?.blockedBy.single.conflicts, isFalse);
    });

    test('keeps the blocked copy when its reload fails', () async {
      final store = _drywallBlockingPaint();

      await BlockedItemRefresher(
        store,
        (_) async => throw const ApiException('Offline'),
      ).refreshItemsBlockedBy('drywall');

      expect(store['paint']?.blockedBy.single.conflicts, isTrue);
    });

    test('does not bring back a blocked item removed meanwhile', () async {
      final store = _drywallBlockingPaint();
      final reload = Completer<WorkItem>();

      final refreshing = BlockedItemRefresher(
        store,
        (_) => reload.future,
      ).refreshItemsBlockedBy('drywall');
      store.remove('paint');
      reload.complete(workItem('paint', version: 2));
      await refreshing;

      expect(store['paint'], isNull);
    });
  });

  group('refresh', () {
    test('reloads the given ids the store holds and skips the rest', () async {
      final store = _drywallBlockingPaint();
      final reloaded = <String>[];

      await BlockedItemRefresher(store, (id) async {
        reloaded.add(id);
        return workItem(id, version: 2);
      }).refresh(['fence', 'unknown']);

      expect(reloaded, ['fence']);
      expect(store['fence']?.version, 2);
      expect(store['unknown'], isNull);
    });

    test('keeps a copy saved while the reload was in flight', () async {
      final store = _drywallBlockingPaint();
      final reload = Completer<WorkItem>();

      final refreshing = BlockedItemRefresher(
        store,
        (_) => reload.future,
      ).refresh(['fence']);
      await store.write(
        'fence',
        send: (_) async => workItem('fence', title: 'Saved', version: 3),
        reload: (_) => throw StateError('No conflict expected'),
      );
      reload.complete(workItem('fence', title: 'Stale', version: 2));
      await refreshing;

      expect(store['fence']?.title, 'Saved');
    });
  });

  // A blocked item's conflict flag depends on its blocker's dates and
  // Done-ness, but changing those doesn't bump the blocked item's version.
  group('attached to a store', () {
    WorkItemStore attached() {
      final store = _drywallBlockingPaint();
      BlockedItemRefresher.attachTo(store);
      return store;
    }

    test('a save that moves the dates reloads the items it blocks', () async {
      final store = attached();
      final reloaded = <String>[];

      await store.write(
        'drywall',
        send: (_) async =>
            workItem('drywall', endDate: DateTime(2026, 11, 17), version: 2),
        reload: (id) async {
          reloaded.add(id);
          return _paintNoLongerConflicting(id);
        },
      );
      await pumpEventQueue();

      expect(reloaded, ['paint']);
      expect(store['paint']?.blockedBy.single.conflicts, isFalse);
    });

    test('a save that moves the item into Done reloads them too', () async {
      final store = attached();
      final reloaded = <String>[];

      await store.write(
        'drywall',
        send: (_) async => workItem(
          'drywall',
          statusId: 'done',
          endDate: DateTime(2026, 11, 20),
          completionDate: DateTime(2026, 11, 10),
          version: 2,
        ),
        reload: (id) async {
          reloaded.add(id);
          return _paintNoLongerConflicting(id);
        },
      );
      await pumpEventQueue();

      expect(reloaded, ['paint']);
    });

    test('a save of another field reloads nothing', () async {
      final store = attached();

      await store.write(
        'drywall',
        send: (_) async => workItem(
          'drywall',
          title: 'Renamed',
          endDate: DateTime(2026, 11, 20),
          version: 2,
        ),
        reload: (_) => throw StateError('No reload expected'),
      );
      await pumpEventQueue();

      expect(store['paint']?.blockedBy.single.conflicts, isTrue);
    });

    test('a failed reload does not fail the save', () async {
      final store = attached();

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
  });
}
