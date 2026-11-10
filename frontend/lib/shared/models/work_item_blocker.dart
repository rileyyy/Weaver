import 'package:flutter/foundation.dart';

/// A live item that blocks a [WorkItem], as the server sends it in
/// `blockedBy`: enough to name it, since it may be in another Space or
/// filtered out of view.
@immutable
class WorkItemBlocker {
  const WorkItemBlocker({
    required this.id,
    required this.number,
    required this.title,
    required this.conflicts,
  });

  factory WorkItemBlocker.fromJson(Map<String, dynamic> json) =>
      WorkItemBlocker(
        id: json['id'] as String,
        number: json['number'] as int,
        title: json['title'] as String,
        conflicts: json['conflicts'] as bool? ?? false,
      );

  final String id;
  final int number;
  final String title;

  /// Whether this link is a dependency conflict (see the glossary). The
  /// server decides, so the client never applies the rule itself.
  final bool conflicts;

  @override
  bool operator ==(Object other) =>
      other is WorkItemBlocker &&
      other.id == id &&
      other.number == number &&
      other.title == title &&
      other.conflicts == conflicts;

  @override
  int get hashCode => Object.hash(id, number, title, conflicts);
}
