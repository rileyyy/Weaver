/// The Hierarchy view's columns, left to right. Every one is resizable from
/// the header; [trailing] are the data columns after the title.
enum HierarchyColumn {
  number,
  title,
  status,
  assignedTo,
  tags;

  static const List<HierarchyColumn> trailing = [status, assignedTo, tags];

  String get headerLabel => switch (this) {
    HierarchyColumn.number => '#',
    HierarchyColumn.title => 'Title',
    HierarchyColumn.status => 'Status',
    HierarchyColumn.assignedTo => 'Assigned To',
    HierarchyColumn.tags => 'Tags',
  };

  double get defaultWidth => switch (this) {
    HierarchyColumn.number => 64,
    HierarchyColumn.title => 280,
    HierarchyColumn.status => 140,
    HierarchyColumn.assignedTo => 160,
    HierarchyColumn.tags => 200,
  };
}
