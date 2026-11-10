import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/views/roadmap/roadmap_arrows.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

HierarchyItem _item(
  String id, {
  DateTime? start,
  DateTime? end,
  List<WorkItemBlocker> blockedBy = const [],
}) => HierarchyItem(
  id: id,
  number: 1,
  parentId: null,
  title: id,
  statusId: 'todo',
  startDate: start,
  endDate: end,
  blockedBy: blockedBy,
);

WorkItemBlocker _blockedBy(String id, {bool conflicts = false}) =>
    WorkItemBlocker(id: id, number: 1, title: id, conflicts: conflicts);

void main() {
  // 14 days over 140px: 10px a day. Rows are 40px tall.
  final windowStart = DateTime(2026, 11, 2);

  List<RoadmapArrow> arrows(List<HierarchyItem> rows) => roadmapArrows(
    rows: rows,
    windowStart: windowStart,
    totalDays: 14,
    timelineWidth: 140,
    rowHeight: 40,
  );

  test('runs from the end of the blocker bar to the start of the blocked '
      'bar, between the row centres', () {
    final result = arrows([
      _item(
        'drywall',
        start: DateTime(2026, 11, 2),
        end: DateTime(2026, 11, 4),
      ),
      _item('trim'),
      _item(
        'paint',
        start: DateTime(2026, 11, 6),
        end: DateTime(2026, 11, 8),
        blockedBy: [_blockedBy('drywall')],
      ),
    ]);

    expect(result, [
      (from: const Offset(30, 20), to: const Offset(40, 100), conflicts: false),
    ]);
  });

  test('carries whether the link conflicts', () {
    final result = arrows([
      _item(
        'paint',
        start: DateTime(2026, 11, 3),
        end: DateTime(2026, 11, 8),
        blockedBy: [_blockedBy('drywall', conflicts: true)],
      ),
      _item(
        'drywall',
        start: DateTime(2026, 11, 2),
        end: DateTime(2026, 11, 5),
      ),
    ]);

    expect(result, [
      (from: const Offset(40, 60), to: const Offset(10, 20), conflicts: true),
    ]);
  });

  test('skips a blocker that is not a visible row', () {
    final result = arrows([
      _item(
        'paint',
        start: DateTime(2026, 11, 6),
        blockedBy: [_blockedBy('collapsed-or-filtered')],
      ),
    ]);

    expect(result, isEmpty);
  });

  test("skips a link whose blocker's end is not on the timeline", () {
    for (final blockerEnd in [
      null,
      DateTime(2026, 11, 16),
      DateTime(2026, 11, 1),
    ]) {
      final result = arrows([
        _item('drywall', start: DateTime(2026, 10, 1), end: blockerEnd),
        _item(
          'paint',
          start: DateTime(2026, 11, 6),
          blockedBy: [_blockedBy('drywall')],
        ),
      ]);

      expect(result, isEmpty, reason: 'blocker ending $blockerEnd');
    }
  });

  test("skips a link whose blocked item's start is not on the timeline", () {
    for (final blockedStart in [
      null,
      DateTime(2026, 11, 1),
      DateTime(2026, 11, 16),
    ]) {
      final result = arrows([
        _item(
          'drywall',
          start: DateTime(2026, 11, 2),
          end: DateTime(2026, 11, 4),
        ),
        _item(
          'paint',
          start: blockedStart,
          end: DateTime(2026, 11, 20),
          blockedBy: [_blockedBy('drywall')],
        ),
      ]);

      expect(result, isEmpty, reason: 'blocked item starting $blockedStart');
    }
  });

  test(
    'a blocker ending on the last day of the window still gets an arrow',
    () {
      final result = arrows([
        _item(
          'drywall',
          start: DateTime(2026, 11, 10),
          end: DateTime(2026, 11, 15),
        ),
        _item(
          'paint',
          start: DateTime(2026, 11, 15),
          blockedBy: [_blockedBy('drywall', conflicts: true)],
        ),
      ]);

      expect(result, [
        (
          from: const Offset(140, 20),
          to: const Offset(130, 60),
          conflicts: true,
        ),
      ]);
    },
  );

  test('skips a blocker whose dates are the wrong way round', () {
    final result = arrows([
      _item(
        'drywall',
        start: DateTime(2026, 11, 10),
        end: DateTime(2026, 11, 5),
      ),
      _item(
        'paint',
        start: DateTime(2026, 11, 6),
        blockedBy: [_blockedBy('drywall')],
      ),
    ]);

    expect(result, isEmpty);
  });

  group('roadmapArrowRoute', () {
    test('steps out of the blocker, down, and into the blocked bar', () {
      final route = roadmapArrowRoute((
        from: const Offset(30, 20),
        to: const Offset(60, 100),
        conflicts: false,
      ), rowHeight: 40);

      expect(route, const [
        Offset(30, 20),
        Offset(38, 20),
        Offset(38, 100),
        Offset(60, 100),
      ]);
    });

    test(
      'doubles back along the row gap when the blocked bar starts first',
      () {
        final route = roadmapArrowRoute((
          from: const Offset(60, 20),
          to: const Offset(30, 100),
          conflicts: true,
        ), rowHeight: 40);

        expect(route, const [
          Offset(60, 20),
          Offset(68, 20),
          Offset(68, 80),
          Offset(22, 80),
          Offset(22, 100),
          Offset(30, 100),
        ]);
      },
    );

    test(
      'doubles back along the gap below the blocked row when it is above',
      () {
        final route = roadmapArrowRoute((
          from: const Offset(60, 100),
          to: const Offset(30, 20),
          conflicts: true,
        ), rowHeight: 40);

        expect(route, const [
          Offset(60, 100),
          Offset(68, 100),
          Offset(68, 40),
          Offset(22, 40),
          Offset(22, 20),
          Offset(30, 20),
        ]);
      },
    );
  });
}
