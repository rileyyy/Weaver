import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/features/board/models/agenda_group.dart';
import 'package:weaver/shared/models/work_item.dart';

/// Groups dated [items] for the Agenda, given the server's [today]: an
/// Overdue group first, then one group per day from today on, in order.
///
/// An open start or end is unbounded on that side, as in the date-window
/// rule (ADR-0003). An unfinished item ending before today is Overdue; a
/// finished one (it has a completion date) is past and left out. Every other
/// item shows once, with its range, under the first day it's on from today
/// on, rather than on each day it spans, so a long item doesn't fill a
/// phone screen. Within a group, the item ending soonest comes first.
List<AgendaGroup> groupAgenda(
  Iterable<WorkItem> items, {
  required DateTime today,
}) {
  final todayDate = dateOnly(today);
  final overdue = <WorkItem>[];
  final byDay = <DateTime, List<WorkItem>>{};
  for (final item in items) {
    final start = item.startDate;
    final end = item.endDate;
    if (start == null && end == null) continue;
    if (end != null && end.isBefore(todayDate)) {
      if (item.completionDate == null) overdue.add(item);
      continue;
    }
    final day = start == null || start.isBefore(todayDate) ? todayDate : start;
    byDay.putIfAbsent(day, () => []).add(item);
  }
  final days = byDay.keys.toList()..sort();
  return [
    if (overdue.isNotEmpty)
      AgendaGroup(day: null, items: overdue..sort(_endingSoonestFirst)),
    for (final day in days)
      AgendaGroup(day: day, items: byDay[day]!..sort(_endingSoonestFirst)),
  ];
}

/// Undated ends last; ties keep their number order.
int _endingSoonestFirst(WorkItem a, WorkItem b) {
  final byEnd = switch ((a.endDate, b.endDate)) {
    (final x?, final y?) => x.compareTo(y),
    (null, null) => 0,
    (null, _) => 1,
    (_, null) => -1,
  };
  return byEnd != 0 ? byEnd : a.number.compareTo(b.number);
}
