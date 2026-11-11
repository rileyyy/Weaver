import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/dates/date_format.dart';
import 'package:weaver/features/board/models/agenda_group.dart';
import 'package:weaver/features/board/state/agenda_groups.dart';

import '../../../shared/fakes/work_items.dart';

final _today = DateTime(2026, 11, 11);

/// Each group as `heading: ids`, e.g. `2026-11-12: a, b`.
List<String> _shape(List<AgendaGroup> groups) => [
  for (final group in groups)
    '${switch (group.day) {
      final day? => formatDate(day),
      null => 'Overdue',
    }}: ${group.items.map((item) => item.id).join(', ')}',
];

void main() {
  test('leaves out undated items and puts unfinished past items under '
      'Overdue', () {
    final groups = groupAgenda([
      workItem('undated'),
      workItem(
        'late',
        startDate: DateTime(2026, 11, 3),
        endDate: DateTime(2026, 11, 10),
      ),
    ], today: _today);

    expect(_shape(groups), ['Overdue: late']);
    expect(groups.single.isOverdue, isTrue);
  });

  test('groups the rest by day, today first, then later days in order', () {
    final groups = groupAgenda([
      workItem('friday', startDate: DateTime(2026, 11, 14)),
      workItem(
        'thursday',
        startDate: DateTime(2026, 11, 13),
        endDate: DateTime(2026, 11, 13),
      ),
      workItem(
        'today',
        startDate: DateTime(2026, 11, 11),
        endDate: DateTime(2026, 11, 11),
      ),
      workItem('late', endDate: DateTime(2026, 11, 1)),
    ], today: _today);

    expect(_shape(groups), [
      'Overdue: late',
      '2026-11-11: today',
      '2026-11-13: thursday',
      '2026-11-14: friday',
    ]);
  });

  // Decided for ticket 24: a multi-day item shows once, with its range,
  // rather than on every day it spans, so a week-long item doesn't fill a
  // phone screen. That's under the first day it's on from today on.
  test('shows a multi-day item once, under the first day it is on from '
      'today', () {
    final groups = groupAgenda([
      workItem(
        'started',
        startDate: DateTime(2026, 11, 9),
        endDate: DateTime(2026, 11, 13),
      ),
      workItem(
        'upcoming',
        startDate: DateTime(2026, 11, 12),
        endDate: DateTime(2026, 11, 20),
      ),
    ], today: _today);

    expect(_shape(groups), ['2026-11-11: started', '2026-11-12: upcoming']);
  });

  // An open side is unbounded, as in the date-window rule (ADR-0003), so
  // an item with only an end is on until then, and one with only a start
  // is on from then.
  test('treats an open start or end as unbounded', () {
    final groups = groupAgenda([
      workItem('due friday', endDate: DateTime(2026, 11, 14)),
      workItem('ongoing', startDate: DateTime(2026, 10, 1)),
    ], today: _today);

    expect(_shape(groups), ['2026-11-11: due friday, ongoing']);
  });

  test('leaves a finished past item out rather than Overdue, but keeps a '
      'finished item that is still on', () {
    final groups = groupAgenda([
      workItem(
        'done last week',
        endDate: DateTime(2026, 11, 4),
        completionDate: DateTime(2026, 11, 4),
      ),
      workItem(
        'done today',
        startDate: DateTime(2026, 11, 11),
        endDate: DateTime(2026, 11, 11),
        completionDate: DateTime(2026, 11, 11),
      ),
    ], today: _today);

    expect(_shape(groups), ['2026-11-11: done today']);
  });
}
