import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/work_item_detail/models/space_move_mapping.dart';

/// What moving a work item to another Space would do (`SpaceMovePlanDto`):
/// how many items go (it and its sub-items), how many custom field values
/// are cleared, and each status, Type and priority's default in the target.
class SpaceMovePlan {
  const SpaceMovePlan({
    required this.itemCount,
    required this.clearedFieldValueCount,
    required this.statuses,
    required this.types,
    required this.priorities,
  });

  factory SpaceMovePlan.fromJson(Map<String, dynamic> json) => SpaceMovePlan(
    itemCount: json['itemCount'] as int,
    clearedFieldValueCount: json['clearedFieldValueCount'] as int,
    statuses: JsonApiClient.listOf(json['statuses'], SpaceMoveMapping.fromJson),
    types: JsonApiClient.listOf(json['types'], SpaceMoveMapping.fromJson),
    priorities: JsonApiClient.listOf(
      json['priorities'],
      SpaceMoveMapping.fromJson,
    ),
  );

  final int itemCount;
  final int clearedFieldValueCount;
  final List<SpaceMoveMapping> statuses;
  final List<SpaceMoveMapping> types;
  final List<SpaceMoveMapping> priorities;
}
