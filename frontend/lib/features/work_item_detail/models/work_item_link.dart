import 'package:weaver/features/work_item_detail/models/work_item_link_relation.dart';

/// A link as seen from the work item currently being viewed: the
/// `linkedWorkItem*` fields are always the other side, and [type] reads
/// from this side. The other item may be in another Space.
class WorkItemLink {
  const WorkItemLink({
    required this.id,
    required this.type,
    required this.linkedWorkItemId,
    required this.linkedWorkItemNumber,
    required this.linkedWorkItemTitle,
    required this.linkedWorkItemSpaceId,
  });

  factory WorkItemLink.fromJson(Map<String, dynamic> json) => WorkItemLink(
    id: json['id'] as String,
    type: WorkItemLinkRelation.fromWire(json['type'] as String),
    linkedWorkItemId: json['linkedWorkItemId'] as String,
    linkedWorkItemNumber: json['linkedWorkItemNumber'] as int,
    linkedWorkItemTitle: json['linkedWorkItemTitle'] as String,
    linkedWorkItemSpaceId: json['linkedWorkItemSpaceId'] as String,
  );

  final String id;
  final WorkItemLinkRelation type;
  final String linkedWorkItemId;
  final int linkedWorkItemNumber;
  final String linkedWorkItemTitle;
  final String linkedWorkItemSpaceId;
}
