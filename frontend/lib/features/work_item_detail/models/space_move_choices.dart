/// What the user confirmed for a move to another Space: where it goes, and
/// for each status, Type and priority of the moving items (by id) which of
/// the target Space's it becomes. A null Type or priority is cleared.
class SpaceMoveChoices {
  const SpaceMoveChoices({
    required this.targetSpaceId,
    required this.parentId,
    required this.statuses,
    required this.types,
    required this.priorities,
  });

  final String targetSpaceId;

  /// Null for the top level of the target Space.
  final String? parentId;
  final Map<String, String> statuses;
  final Map<String, String?> types;
  final Map<String, String?> priorities;
}
