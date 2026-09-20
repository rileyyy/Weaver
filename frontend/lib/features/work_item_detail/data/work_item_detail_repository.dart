import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/models/work_item_comment.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';

abstract class WorkItemDetailRepository {
  Future<WorkItemDetail> getItem(String id);

  /// Deletes the work item identified by [id]. If it has children,
  /// [cascade] must be true or the backend rejects the delete — a subtree
  /// is never silently dropped.
  Future<void> deleteItem(String id, {bool cascade = false});

  Future<List<BoardStatus>> loadStatuses();

  Future<List<WorkItemLayer>> loadLayers();

  Future<List<AuthUser>> loadUsers();

  Future<WorkItemDetail> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
  });

  Future<WorkItemDetail> assign(String id, String? userId);

  Future<WorkItemDetail> reschedule(String id, DateTime? startDate, DateTime? endDate);

  Future<List<WorkItemComment>> loadComments(String workItemId);

  Future<WorkItemComment> addComment(String workItemId, String body);

  Future<WorkItemComment> updateComment(String commentId, String body);

  Future<void> deleteComment(String commentId);

  Future<List<WorkItemLink>> loadLinks(String workItemId);

  Future<WorkItemLink> addLink(String workItemId, String targetWorkItemId);

  Future<void> deleteLink(String linkId);
}
