import 'package:weaver/core/network/api_dates.dart';

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

  factory WorkItemComment.fromJson(Map<String, dynamic> json) =>
      WorkItemComment(
        id: json['id'] as String,
        workItemId: json['workItemId'] as String,
        authorUserId: json['authorUserId'] as String,
        authorUsername: json['authorUsername'] as String,
        body: json['body'] as String,
        createdAt: parseApiTimestamp(json['createdAtUtc'] as String),
        updatedAt: parseOptionalApiTimestamp(json['updatedAtUtc']),
      );

  final String id;
  final String workItemId;
  final String authorUserId;
  final String authorUsername;
  final String body;
  final DateTime createdAt;
  final DateTime? updatedAt;
}
