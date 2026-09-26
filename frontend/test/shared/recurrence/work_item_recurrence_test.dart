import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

import 'fake_recurrence_repository.dart';

void main() {
  test('fromJson reads the API shape', () {
    final parsed = WorkItemRecurrence.fromJson({
      'workItemId': 'item-1',
      'workItemNumber': 12,
      'workItemTitle': 'Report',
      'parentId': 'lane-1',
      'parentTitle': 'Finance',
      'frequency': 'BiWeekly',
      'daysOfWeek': ['Monday', 'Sunday'],
      'startDate': '2026-09-28',
      'endDate': null,
      'nextOccurrence': '2026-10-04',
    });

    expect(parsed.workItemNumber, 12);
    expect(parsed.parentTitle, 'Finance');
    expect(parsed.frequency, RecurrenceFrequency.biWeekly);
    expect(parsed.weekdays, {DateTime.monday, DateTime.sunday});
    expect(parsed.startDate, DateTime(2026, 9, 28));
    expect(parsed.endDate, isNull);
    expect(parsed.nextOccurrence, DateTime(2026, 10, 4));
  });

  test('fromJson rejects an unknown frequency', () {
    expect(
      () => WorkItemRecurrence.fromJson({
        'workItemId': 'item-1',
        'workItemNumber': 1,
        'workItemTitle': 'Report',
        'frequency': 'Hourly',
        'daysOfWeek': <String>[],
        'startDate': '2026-09-28',
      }),
      throwsFormatException,
    );
  });

  test('summary lists weekdays for week-based schedules', () {
    final weekly = recurrence(
      weekdays: const {DateTime.thursday, DateTime.monday},
    );

    expect(weekly.summary, 'Weekly on Mon, Thu');
  });

  test('summary gives the day of month for month-based schedules', () {
    final monthly = recurrence(
      frequency: RecurrenceFrequency.quarterly,
      weekdays: const {},
      startDate: DateTime(2026, 1, 15),
    );

    expect(monthly.summary, 'Quarterly on day 15');
  });

  test('toDraft carries the schedule over', () {
    final saved = recurrence(endDate: DateTime(2026, 12, 31));

    final draft = saved.toDraft();

    expect(draft.frequency, saved.frequency);
    expect(draft.weekdays, saved.weekdays);
    expect(draft.startDate, saved.startDate);
    expect(draft.endDate, saved.endDate);
  });
}
