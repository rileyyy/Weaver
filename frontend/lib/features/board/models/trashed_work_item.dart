import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/shared/models/user_kind.dart';

/// One delete in the Trash: the item it was made on, when, by whom, and how
/// many items (it and its sub-items) restoring it brings back.
class TrashedWorkItem {
  const TrashedWorkItem({
    required this.workItemId,
    required this.number,
    required this.title,
    required this.spaceName,
    required this.trashedAt,
    required this.itemCount,
    required this.deletedByName,
    required this.deletedByKind,
    required this.deletedByActorLabel,
  });

  factory TrashedWorkItem.fromJson(Map<String, dynamic> json) {
    final item = json['item'] as Map<String, dynamic>;
    final kind = json['deletedByKind'] as String?;
    return TrashedWorkItem(
      workItemId: item['id'] as String,
      number: item['number'] as int,
      title: item['title'] as String,
      spaceName: item['spaceName'] as String?,
      trashedAt: parseApiTimestamp(json['trashedAtUtc'] as String),
      itemCount: json['itemCount'] as int,
      deletedByName: json['deletedByName'] as String?,
      deletedByKind: kind == null ? null : UserKind.fromWire(kind),
      deletedByActorLabel: json['deletedByActorLabel'] as String?,
    );
  }

  final String workItemId;
  final int number;
  final String title;
  final String? spaceName;
  final DateTime trashedAt;

  /// The item plus every sub-item deleted with it.
  final int itemCount;

  /// Null when history no longer says who deleted it.
  final String? deletedByName;
  final UserKind? deletedByKind;

  /// What an agent said it was acting as, if it said.
  final String? deletedByActorLabel;
}
