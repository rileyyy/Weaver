/// The Hierarchy view's columns, left to right. Every one is resizable from
/// the header.
enum HierarchyColumn {
  number,
  title,
  status,
  assignedTo,
  tags,

  /// Roll-ups: each field's own value and its sub-items' total. Only shown
  /// when the Space has a field that rolls up.
  totals;

  /// The columns to show, leaving out [totals] when there's nothing to put
  /// in it.
  static List<HierarchyColumn> shown({required bool withTotals}) => [
    for (final column in values)
      if (column != totals || withTotals) column,
  ];

  String get headerLabel => switch (this) {
    HierarchyColumn.number => '#',
    HierarchyColumn.title => 'Title',
    HierarchyColumn.status => 'Status',
    HierarchyColumn.assignedTo => 'Assigned To',
    HierarchyColumn.tags => 'Tags',
    HierarchyColumn.totals => 'Totals',
  };

  double get defaultWidth => switch (this) {
    HierarchyColumn.number => 64,
    HierarchyColumn.title => 280,
    HierarchyColumn.status => 140,
    HierarchyColumn.assignedTo => 160,
    HierarchyColumn.tags => 200,
    HierarchyColumn.totals => 260,
  };
}
