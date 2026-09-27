import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

WorkItemRecurrence recurrence({
  String workItemId = 'item-1',
  int number = 1,
  String title = 'Report',
  String? parentTitle,
  RecurrenceFrequency frequency = RecurrenceFrequency.weekly,
  Set<int> weekdays = const {DateTime.monday},
  DateTime? startDate,
  DateTime? endDate,
  DateTime? nextOccurrence,
}) => WorkItemRecurrence(
  workItemId: workItemId,
  workItemNumber: number,
  workItemTitle: title,
  parentTitle: parentTitle,
  frequency: frequency,
  weekdays: weekdays,
  startDate: startDate ?? DateTime(2026, 9, 28),
  endDate: endDate,
  nextOccurrence: nextOccurrence ?? DateTime(2026, 9, 28),
);

/// Holds schedules in memory. Set [error] to make every call fail with it.
class FakeRecurrenceRepository implements RecurrenceRepository {
  FakeRecurrenceRepository([Iterable<WorkItemRecurrence> initial = const []])
    : schedules = {for (final r in initial) r.workItemId: r};

  final Map<String, WorkItemRecurrence> schedules;
  ApiException? error;
  final List<(String, RecurrenceDraft)> saveCalls = [];
  final List<String> removeCalls = [];

  @override
  Future<WorkItemRecurrence?> get(String workItemId) async {
    if (error case final e?) throw e;
    return schedules[workItemId];
  }

  @override
  Future<List<WorkItemRecurrence>> list() async {
    if (error case final e?) throw e;
    return schedules.values.toList();
  }

  @override
  Future<WorkItemRecurrence> save(
    String workItemId,
    RecurrenceDraft draft,
  ) async {
    saveCalls.add((workItemId, draft));
    if (error case final e?) throw e;
    final existing = schedules[workItemId];
    return schedules[workItemId] = recurrence(
      workItemId: workItemId,
      number: existing?.workItemNumber ?? 1,
      title: existing?.workItemTitle ?? 'Report',
      frequency: draft.frequency,
      weekdays: draft.frequency.isWeekBased ? draft.weekdays : const {},
      startDate: draft.startDate,
      endDate: draft.endDate,
      nextOccurrence: draft.startDate,
    );
  }

  @override
  Future<void> remove(String workItemId) async {
    removeCalls.add(workItemId);
    if (error case final e?) throw e;
    schedules.remove(workItemId);
  }
}
