/// Columns shown after the title column, in order. A future milestone could
/// make this user-configurable; for now it's fixed to Status, Assigned To,
/// then Tags.
enum HierarchyColumn {
  status,
  assignedTo,
  tags;

  String get headerLabel => switch (this) {
    HierarchyColumn.status => 'Status',
    HierarchyColumn.assignedTo => 'Assigned To',
    HierarchyColumn.tags => 'Tags',
  };
}
