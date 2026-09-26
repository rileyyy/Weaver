/// Schedule dates (a work item's start/end) are calendar days, not instants:
/// the API sends and expects `yyyy-MM-dd`, and the client holds them as local
/// midnight so they read back exactly as picked in any time zone. Sending a
/// picked day as `toUtc()` used to shift it to the previous day east of UTC.
DateTime? parseCalendarDate(Object? value) {
  if (value == null) return null;
  final parsed = DateTime.parse(value as String);
  return DateTime(parsed.year, parsed.month, parsed.day);
}

String? formatCalendarDate(DateTime? date) {
  if (date == null) return null;
  final year = date.year.toString().padLeft(4, '0');
  final month = date.month.toString().padLeft(2, '0');
  final day = date.day.toString().padLeft(2, '0');
  return '$year-$month-$day';
}

/// Every other API timestamp (created/updated times) is a real instant in
/// UTC; convert it to local time so it displays on the user's own day.
DateTime parseApiTimestamp(String value) => DateTime.parse(value).toLocal();

DateTime? parseOptionalApiTimestamp(Object? value) =>
    value == null ? null : parseApiTimestamp(value as String);
