class WorkItemComment {
  const WorkItemComment({
    required this.id,
    required this.workItemId,
    required this.authorUserId,
    required this.authorUsername,
    required this.body,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String workItemId;
  final String authorUserId;
  final String authorUsername;
  final String body;
  final DateTime createdAt;
  final DateTime? updatedAt;
}
