import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/recurrence_frequency.dart';

// 2026-09-30 is a Wednesday.
final _wednesday = DateTime(2026, 9, 30);

RecurrenceDraft _draft({
  RecurrenceFrequency frequency = RecurrenceFrequency.weekly,
  Set<int> weekdays = const {DateTime.monday},
  DateTime? startDate,
  DateTime? endDate,
}) => RecurrenceDraft(
  frequency: frequency,
  weekdays: weekdays,
  startDate: startDate ?? _wednesday,
  endDate: endDate,
);

void main() {
  test('startingOn repeats weekly on that weekday with no end', () {
    final draft = RecurrenceDraft.startingOn(_wednesday);

    expect(draft.frequency, RecurrenceFrequency.weekly);
    expect(draft.weekdays, {DateTime.wednesday});
    expect(draft.endDate, isNull);
    expect(draft.validationError, isNull);
  });

  group('validationError', () {
    test('requires a weekday for week-based frequencies', () {
      expect(
        _draft(weekdays: const {}).validationError,
        'Choose at least one day of the week to repeat on.',
      );
      expect(
        _draft(
          frequency: RecurrenceFrequency.biWeekly,
          weekdays: const {},
        ).validationError,
        isNotNull,
      );
    });

    test('needs no weekday for month-based frequencies', () {
      expect(
        _draft(
          frequency: RecurrenceFrequency.quarterly,
          weekdays: const {},
        ).validationError,
        isNull,
      );
    });

    test('rejects an end before the start but allows the same day', () {
      expect(
        _draft(endDate: DateTime(2026, 9, 29)).validationError,
        "The repeat end date can't be before its start date.",
      );
      expect(_draft(endDate: _wednesday).validationError, isNull);
    });
  });

  group('toJson', () {
    test('sends weekdays Monday first with their API names', () {
      final json = _draft(
        weekdays: const {DateTime.sunday, DateTime.monday, DateTime.friday},
        endDate: DateTime(2026, 12, 31),
      ).toJson();

      expect(json, {
        'frequency': 'Weekly',
        'daysOfWeek': ['Monday', 'Friday', 'Sunday'],
        'startDate': '2026-09-30',
        'endDate': '2026-12-31',
      });
    });

    test('omits weekdays for month-based frequencies', () {
      final json = _draft(
        frequency: RecurrenceFrequency.monthly,
        weekdays: const {DateTime.monday},
      ).toJson();

      expect(json['frequency'], 'Monthly');
      expect(json['daysOfWeek'], isEmpty);
      expect(json['endDate'], isNull);
    });

    test('sends bi-weekly under its API name', () {
      expect(
        _draft(frequency: RecurrenceFrequency.biWeekly).toJson()['frequency'],
        'BiWeekly',
      );
    });
  });

  test('toggleWeekday adds and removes a day', () {
    final added = _draft().toggleWeekday(DateTime.thursday);
    final removed = added.toggleWeekday(DateTime.monday);

    expect(added.weekdays, {DateTime.monday, DateTime.thursday});
    expect(removed.weekdays, {DateTime.thursday});
  });

  group('withFrequency', () {
    test('keeps chosen weekdays through a month-based frequency', () {
      final draft = _draft(weekdays: const {DateTime.friday})
          .withFrequency(RecurrenceFrequency.monthly)
          .withFrequency(RecurrenceFrequency.weekly);

      expect(draft.weekdays, {DateTime.friday});
    });

    test('picks the start weekday when switching to weekly with none', () {
      final draft = _draft(
        frequency: RecurrenceFrequency.yearly,
        weekdays: const {},
      ).withFrequency(RecurrenceFrequency.biWeekly);

      expect(draft.weekdays, {DateTime.wednesday});
      expect(draft.validationError, isNull);
    });
  });

  test('copyWith can clear the end date', () {
    final draft = _draft(endDate: DateTime(2026, 12, 1));

    expect(draft.copyWith(endDate: () => null).endDate, isNull);
    expect(draft.copyWith().endDate, DateTime(2026, 12, 1));
  });

  group('isEquivalentTo', () {
    test('ignores weekdays for month-based frequencies', () {
      final a = _draft(
        frequency: RecurrenceFrequency.monthly,
        weekdays: const {DateTime.monday},
      );
      final b = _draft(
        frequency: RecurrenceFrequency.monthly,
        weekdays: const {},
      );

      expect(a.isEquivalentTo(b), isTrue);
    });

    test('compares weekdays, dates and frequency otherwise', () {
      final base = _draft();

      expect(base.isEquivalentTo(_draft()), isTrue);
      expect(
        base.isEquivalentTo(_draft(weekdays: const {DateTime.friday})),
        isFalse,
      );
      expect(base.isEquivalentTo(_draft(endDate: DateTime(2027))), isFalse);
      expect(
        base.isEquivalentTo(_draft(frequency: RecurrenceFrequency.biWeekly)),
        isFalse,
      );
    });
  });
}
