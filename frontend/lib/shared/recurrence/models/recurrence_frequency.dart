enum RecurrenceFrequency { weekly, biWeekly, monthly, quarterly, yearly }

RecurrenceFrequency recurrenceFrequencyFromWire(String value) =>
    switch (value) {
      'Weekly' => RecurrenceFrequency.weekly,
      'BiWeekly' => RecurrenceFrequency.biWeekly,
      'Monthly' => RecurrenceFrequency.monthly,
      'Quarterly' => RecurrenceFrequency.quarterly,
      'Yearly' => RecurrenceFrequency.yearly,
      _ => throw FormatException('Unknown repeat frequency "$value".'),
    };

extension RecurrenceFrequencyWire on RecurrenceFrequency {
  String get label => switch (this) {
    RecurrenceFrequency.weekly => 'Weekly',
    RecurrenceFrequency.biWeekly => 'Bi-weekly',
    RecurrenceFrequency.monthly => 'Monthly',
    RecurrenceFrequency.quarterly => 'Quarterly',
    RecurrenceFrequency.yearly => 'Yearly',
  };

  String toWire() => switch (this) {
    RecurrenceFrequency.weekly => 'Weekly',
    RecurrenceFrequency.biWeekly => 'BiWeekly',
    RecurrenceFrequency.monthly => 'Monthly',
    RecurrenceFrequency.quarterly => 'Quarterly',
    RecurrenceFrequency.yearly => 'Yearly',
  };

  /// Weekly and bi-weekly repeat on chosen weekdays; the others repeat on
  /// the start date's day of the month and ignore weekdays.
  bool get isWeekBased =>
      this == RecurrenceFrequency.weekly ||
      this == RecurrenceFrequency.biWeekly;
}
