import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/models/weekdays.dart';

/// A work item's saved repeat schedule, with enough of the work item to
/// list it on its own.
class WorkItemRecurrence {
  const WorkItemRecurrence({
    required this.workItemId,
    required this.workItemNumber,
    required this.workItemTitle,
    required this.parentTitle,
    required this.frequency,
    required this.weekdays,
    required this.startDate,
    required this.endDate,
    required this.nextOccurrence,
  });

  factory WorkItemRecurrence.fromJson(Map<String, dynamic> json) =>
      WorkItemRecurrence(
        workItemId: json['workItemId'] as String,
        workItemNumber: json['workItemNumber'] as int,
        workItemTitle: json['workItemTitle'] as String,
        parentTitle: json['parentTitle'] as String?,
        frequency: recurrenceFrequencyFromWire(json['frequency'] as String),
        weekdays: {
          for (final day in json['daysOfWeek'] as List<dynamic>)
            weekdayFromWire(day as String),
        },
        startDate: parseCalendarDate(json['startDate'])!,
        endDate: parseCalendarDate(json['endDate']),
        nextOccurrence: parseCalendarDate(json['nextOccurrence']),
      );

  final String workItemId;
  final int workItemNumber;
  final String workItemTitle;

  /// Null for a top-level item.
  final String? parentTitle;
  final RecurrenceFrequency frequency;

  /// [DateTime.monday]..[DateTime.sunday]; empty for month-based frequencies.
  final Set<int> weekdays;
  final DateTime startDate;
  final DateTime? endDate;

  /// Null once the schedule has ended.
  final DateTime? nextOccurrence;

  RecurrenceDraft toDraft() => RecurrenceDraft(
    frequency: frequency,
    weekdays: weekdays,
    startDate: startDate,
    endDate: endDate,
  );

  /// e.g. "Weekly on Mon, Thu" or "Monthly on day 15".
  String get summary {
    if (frequency.isWeekBased) {
      final days = [
        for (final day in weekdaysMondayFirst)
          if (weekdays.contains(day)) weekdayShortName(day),
      ];
      return '${frequency.label} on ${days.join(', ')}';
    }
    return '${frequency.label} on day ${startDate.day}';
  }
}
