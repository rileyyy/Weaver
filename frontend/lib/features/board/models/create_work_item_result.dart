class CreateWorkItemResult {
  const CreateWorkItemResult({
    required this.title,
    this.description,
    required this.parentId,
  });

  final String title;
  final String? description;

  /// Null makes the new item a top-level item with no parent at all.
  final String? parentId;
}
