/// Formats [date] as `YYYY-MM-DD`, in whatever the local system's calendar
/// fields say — good enough for a compact label, not locale-aware display.
String formatDate(DateTime date) {
  final year = date.year.toString().padLeft(4, '0');
  final month = date.month.toString().padLeft(2, '0');
  final day = date.day.toString().padLeft(2, '0');
  return '$year-$month-$day';
}
