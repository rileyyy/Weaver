import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

abstract class WorkItemDetailRepository {
  Future<WorkItemDetail> getItem(String id);

  /// This item's direct children, for the detail screen's "Sub-Items"
  /// section — the same `parentId`-scoped `/work-items` query the board
  /// uses for a swimlane's cards.
  Future<List<WorkItemChildSummary>> loadChildren(String parentId);

  /// Deletes the work item identified by [id]. If it has children,
  /// [cascade] must be true or the backend rejects the delete — a subtree
  /// is never silently dropped.
  Future<void> deleteItem(String id, {bool cascade = false});

  Future<List<BoardStatus>> loadStatuses();

  Future<List<WorkItemLayer>> loadLayers();

  Future<List<AuthUser>> loadUsers();

  /// [expectedVersion] is the [WorkItemDetail.version] the edit was based
  /// on; the save fails with a 409 [ApiException] if the item has changed
  /// since. The same applies to [reschedule] and [updateTags].
  Future<WorkItemDetail> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
    required int expectedVersion,
  });

  Future<WorkItemDetail> assign(String id, String? userId);

  Future<WorkItemDetail> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  });

  /// Replaces this work item's full tag list.
  Future<WorkItemDetail> updateTags(
    String id,
    List<String> tags, {
    required int expectedVersion,
  });

  Future<List<WorkItemComment>> loadComments(String workItemId);

  Future<WorkItemComment> addComment(String workItemId, String body);

  Future<WorkItemComment> updateComment(String commentId, String body);

  Future<void> deleteComment(String commentId);

  Future<List<WorkItemLink>> loadLinks(String workItemId);

  Future<WorkItemLink> addLink(String workItemId, String targetWorkItemId);

  Future<void> deleteLink(String linkId);
}
