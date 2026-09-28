/// Formats [date] as `YYYY-MM-DD`, in whatever the local system's calendar
/// fields say — good enough for a compact label, not locale-aware display.
String formatDate(DateTime date) {
  final year = date.year.toString().padLeft(4, '0');
  final month = date.month.toString().padLeft(2, '0');
  final day = date.day.toString().padLeft(2, '0');
  return '$year-$month-$day';
}

/// Formats [time]'s clock time as 24-hour `HH:mm`, on the same terms as
/// [formatDate].
String formatTime(DateTime time) {
  final hour = time.hour.toString().padLeft(2, '0');
  final minute = time.minute.toString().padLeft(2, '0');
  return '$hour:$minute';
}
