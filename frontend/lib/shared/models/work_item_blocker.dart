import 'package:flutter/foundation.dart';
import 'package:weaver/core/network/api_dates.dart';

/// A live item that blocks a [WorkItem], as the server sends it in
/// `blockedBy`: enough to name it (it may be in another Space, or filtered
/// out of view) and to draw its Roadmap arrow.
@immutable
class WorkItemBlocker {
  const WorkItemBlocker({
    required this.id,
    required this.number,
    required this.title,
    required this.spaceId,
    required this.endDate,
    required this.conflicts,
  });

  factory WorkItemBlocker.fromJson(Map<String, dynamic> json) =>
      WorkItemBlocker(
        id: json['id'] as String,
        number: json['number'] as int,
        title: json['title'] as String,
        spaceId: json['spaceId'] as String,
        endDate: parseCalendarDate(json['endDate']),
        conflicts: json['conflicts'] as bool? ?? false,
      );

  final String id;
  final int number;
  final String title;
  final String spaceId;
  final DateTime? endDate;

  /// Whether this link is a dependency conflict (see the glossary). The
  /// server decides, so the client never applies the rule itself.
  final bool conflicts;

  @override
  bool operator ==(Object other) =>
      other is WorkItemBlocker &&
      other.id == id &&
      other.number == number &&
      other.title == title &&
      other.spaceId == spaceId &&
      other.endDate == endDate &&
      other.conflicts == conflicts;

  @override
  int get hashCode =>
      Object.hash(id, number, title, spaceId, endDate, conflicts);
}
