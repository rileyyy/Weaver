import 'package:flutter/foundation.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';
import 'package:weaver/shared/recurrence/models/weekdays.dart';

/// Repeat settings being edited, before they're saved. Keeps the chosen
/// weekdays even while a month-based frequency is selected, so switching
/// back to weekly doesn't lose them; [toJson] only sends them when they
/// apply.
@immutable
class RecurrenceDraft {
  const RecurrenceDraft({
    required this.frequency,
    required this.weekdays,
    required this.startDate,
    required this.endDate,
  });

  /// Weekly, on [startDate]'s weekday, with no end.
  factory RecurrenceDraft.startingOn(DateTime startDate) => RecurrenceDraft(
    frequency: RecurrenceFrequency.weekly,
    weekdays: {startDate.weekday},
    startDate: startDate,
    endDate: null,
  );

  final RecurrenceFrequency frequency;

  /// [DateTime.monday]..[DateTime.sunday].
  final Set<int> weekdays;
  final DateTime startDate;
  final DateTime? endDate;

  /// Why this can't be saved, or null if it can. Mirrors the backend's
  /// `RecurrenceSchedule.Create`, so the form can say so before a request.
  String? get validationError {
    if (frequency.isWeekBased && weekdays.isEmpty) {
      return 'Choose at least one day of the week to repeat on.';
    }
    final end = endDate;
    if (end != null && end.isBefore(startDate)) {
      return "The repeat end date can't be before its start date.";
    }
    return null;
  }

  RecurrenceDraft copyWith({
    RecurrenceFrequency? frequency,
    Set<int>? weekdays,
    DateTime? startDate,
    DateTime? Function()? endDate,
  }) => RecurrenceDraft(
    frequency: frequency ?? this.frequency,
    weekdays: weekdays ?? this.weekdays,
    startDate: startDate ?? this.startDate,
    endDate: endDate != null ? endDate() : this.endDate,
  );

  /// Switching to a week-based frequency with no days chosen (e.g. from a
  /// saved monthly schedule, which stores none) picks the start date's
  /// weekday rather than starting out invalid.
  RecurrenceDraft withFrequency(RecurrenceFrequency frequency) => copyWith(
    frequency: frequency,
    weekdays: frequency.isWeekBased && weekdays.isEmpty
        ? {startDate.weekday}
        : null,
  );

  RecurrenceDraft toggleWeekday(int weekday) => copyWith(
    weekdays: weekdays.contains(weekday)
        ? ({...weekdays}..remove(weekday))
        : {...weekdays, weekday},
  );

  Map<String, Object?> toJson() => {
    'frequency': frequency.toWire(),
    'daysOfWeek': [
      if (frequency.isWeekBased)
        for (final day in weekdaysMondayFirst)
          if (weekdays.contains(day)) weekdayToWire(day),
    ],
    'startDate': formatCalendarDate(startDate),
    'endDate': formatCalendarDate(endDate),
  };

  /// Whether saving this would change nothing compared with [other].
  /// Weekdays only count when they apply.
  bool isEquivalentTo(RecurrenceDraft other) =>
      frequency == other.frequency &&
      startDate == other.startDate &&
      endDate == other.endDate &&
      (!frequency.isWeekBased || setEquals(weekdays, other.weekdays));
}
