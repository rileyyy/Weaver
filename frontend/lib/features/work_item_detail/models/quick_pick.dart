/// A When the dialog offers as one tap. The server resolves each to dates,
/// because only it knows "today" in the server's time zone.
enum QuickPick {
  thisWeekend('ThisWeekend', 'This weekend'),
  nextWeek('NextWeek', 'Next week'),
  nextMonth('NextMonth', 'Next month');

  const QuickPick(this.wireName, this.label);

  final String wireName;
  final String label;

  static QuickPick fromWire(String value) =>
      values.firstWhere((pick) => pick.wireName == value);
}
