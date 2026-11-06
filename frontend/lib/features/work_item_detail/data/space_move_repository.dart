import 'package:weaver/features/work_item_detail/models/space_move_choices.dart';
import 'package:weaver/features/work_item_detail/models/space_move_plan.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// Everything the Move to Space dialog reads and writes.
abstract class SpaceMoveRepository {
  Future<List<Space>> loadSpaces();

  Future<List<WorkItemStatus>> loadStatuses(String spaceId);

  Future<List<WorkItemType>> loadTypes(String spaceId);

  Future<List<WorkItemPriority>> loadPriorities(String spaceId);

  /// Items of [spaceId] matching [text], to pick the new parent from.
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text);

  Future<WorkItem> getItem(String id);

  /// The server's defaults for moving [id] and its sub-items to
  /// [targetSpaceId]. Changes nothing.
  Future<SpaceMovePlan> planMove(String id, String targetSpaceId);

  /// Moves [id] and its sub-items as [choices] say, in one step. Fails with
  /// a 409 if the item changed since [expectedVersion].
  Future<WorkItem> moveToSpace(
    String id,
    SpaceMoveChoices choices, {
    required int expectedVersion,
  });
}
