/// A Gantt-style timeline: how far zoomed out the timeline is, expressed as
/// how many columns it shows and how many days each column spans (for
/// [quarter]/[year], a column is a week/month rather than a single day, so
/// the whole window stays a manageable number of columns wide).
enum RoadmapTimeframe {
  week,
  fortnight,
  month,
  quarter,
  year;

  String get label => switch (this) {
    RoadmapTimeframe.week => 'Week',
    RoadmapTimeframe.fortnight => 'Fortnight',
    RoadmapTimeframe.month => 'Month',
    RoadmapTimeframe.quarter => 'Quarter',
    RoadmapTimeframe.year => 'Year',
  };

  int get unitCount => switch (this) {
    RoadmapTimeframe.week => 7,
    RoadmapTimeframe.fortnight => 14,
    RoadmapTimeframe.month => 30,
    RoadmapTimeframe.quarter => 13,
    RoadmapTimeframe.year => 12,
  };

  /// Calendar days per column. [quarter] and [year] use approximate
  /// week/month lengths (7 and 30 days) rather than each unit's true
  /// variable length — good enough for a zoomed-out overview, not a
  /// day-accurate calendar at that scale.
  int get daysPerUnit => switch (this) {
    RoadmapTimeframe.week ||
    RoadmapTimeframe.fortnight ||
    RoadmapTimeframe.month => 1,
    RoadmapTimeframe.quarter => 7,
    RoadmapTimeframe.year => 30,
  };

  int get totalDays => unitCount * daysPerUnit;

  String unitLabel(DateTime unitStart) => switch (this) {
    RoadmapTimeframe.week ||
    RoadmapTimeframe.fortnight ||
    RoadmapTimeframe.month => '${unitStart.day}',
    RoadmapTimeframe.quarter =>
      '${_monthAbbreviations[unitStart.month - 1]} ${unitStart.day}',
    RoadmapTimeframe.year => _monthAbbreviations[unitStart.month - 1],
  };
}

const List<String> _monthAbbreviations = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];
