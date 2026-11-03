import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/models/work_item_child_summary.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_layer.dart';
import 'package:weaver/shared/models/work_item_status.dart';

abstract class WorkItemDetailRepository {
  Future<WorkItem> getItem(String id);

  /// This item's direct children, for the detail screen's "Sub-Items"
  /// section — the same `parentId`-scoped `/work-items` query the board
  /// uses for a swimlane's cards.
  Future<List<WorkItemChildSummary>> loadChildren(String parentId);

  /// Deletes the work item identified by [id]. If it has children,
  /// [cascade] must be true or the backend rejects the delete — a subtree
  /// is never silently dropped.
  Future<void> deleteItem(
    String id, {
    bool cascade = false,
    required int expectedVersion,
  });

  Future<List<WorkItemStatus>> loadStatuses();

  Future<List<WorkItemLayer>> loadLayers();

  Future<List<WorkItemPriority>> loadPriorities();

  Future<List<User>> loadUsers();

  /// [expectedVersion] is the [WorkItem.version] the edit was based
  /// on; the save fails with a 409 [ApiException] if the item has changed
  /// since. Every write here takes one, including [deleteItem].
  Future<WorkItem> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required String? priorityId,
    required int expectedVersion,
  });

  /// Saves just the description, leaving the title, layer and priority as
  /// they are on the server, for edits that don't come from the details
  /// form (ticking a task).
  Future<WorkItem> updateDescription(
    String id,
    String description, {
    required int expectedVersion,
  });

  Future<WorkItem> assign(
    String id,
    String? userId, {
    required int expectedVersion,
  });

  Future<WorkItem> reschedule(
    String id,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  });

  /// Replaces this work item's full tag list.
  Future<WorkItem> updateTags(
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
