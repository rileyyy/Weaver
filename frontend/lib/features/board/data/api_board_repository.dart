import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/user.dart';

/// Board data backed by the REST API. Swimlanes are the direct children of
/// whichever scope item [loadBoard] is asked for (the first board's scope
/// item, or top-level items, by default); each swimlane's cards are that
/// swimlane's own direct children — mirroring `Board`'s doc comment on the
/// backend.
@LazySingleton(as: BoardRepository)
class ApiBoardRepository implements BoardRepository {
  ApiBoardRepository(
    http.Client client,
    @Named('apiBaseUrl') String baseUrl,
    this._statuses,
    this._users,
  ) : _api = JsonApiClient(client, baseUrl);

  final JsonApiClient _api;
  final StatusRepository _statuses;
  final UserDirectoryRepository _users;

  @override
  Future<String?> loadRootScopeItemId() => _api.get('/boards', (json) {
    final boards = json as List<dynamic>;
    if (boards.isEmpty) return null;
    return (boards.first as Map<String, dynamic>)['scopeItemId'] as String?;
  }, failureMessage: 'Failed to load boards');

  @override
  Future<BoardData> loadBoard(String? scopeItemId) async {
    // Sequential on purpose: a record .wait would wrap a failure in
    // ParallelWaitError instead of the ApiException callers handle, and
    // statuses are cached after the first load anyway.
    final statuses = await _statuses.loadStatuses();
    final swimlaneItems = await _loadChildren(scopeItemId);

    final swimlanes = await Future.wait([
      for (final parent in swimlaneItems) _loadSwimlane(parent),
    ]);

    return BoardData(statuses: statuses, swimlanes: swimlanes);
  }

  @override
  Future<List<HierarchyItem>> loadAllItems() => _api.get(
    '/work-items/all',
    (json) => JsonApiClient.listOf(json, HierarchyItem.fromJson),
    failureMessage: 'Failed to load work items',
  );

  @override
  Future<List<User>> loadUsers() => _users.loadUsers();

  @override
  Future<void> changeStatus(String cardId, String newStatusId) =>
      _api.postIgnoringBody('/work-items/$cardId/status', {
        'statusId': newStatusId,
      }, failureMessage: 'Failed to change status');

  @override
  Future<void> reparentItem(String itemId, String newParentId) =>
      _api.postIgnoringBody('/work-items/$itemId/parent', {
        'parentId': newParentId,
      }, failureMessage: 'Failed to move item');

  @override
  Future<void> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate,
  ) => _api.postIgnoringBody('/work-items/$itemId/schedule', {
    'startDate': formatCalendarDate(startDate),
    'endDate': formatCalendarDate(endDate),
  }, failureMessage: 'Failed to reschedule item');

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) => _api.postIgnoringBody('/work-items', {
    'title': title,
    'description': description,
    'parentId': parentId,
    'statusId': statusId,
  }, failureMessage: 'Failed to create work item');

  @override
  Future<void> assign(String workItemId, String? userId) =>
      _api.postIgnoringBody(
        '/work-items/$workItemId/assignee',
        {'userId': userId},
        failureMessage: 'Failed to assign work item',
      );

  @override
  Future<void> setTags(String workItemId, List<String> tags) =>
      _api.postIgnoringBody('/work-items/$workItemId/tags', {
        'tags': tags,
      }, failureMessage: 'Failed to set tags');

  Future<Swimlane> _loadSwimlane(Map<String, dynamic> parent) async {
    final parentId = parent['id'] as String;
    final cards = await _api.get(
      '/work-items',
      (json) => JsonApiClient.listOf(json, WorkItemCard.fromJson),
      query: {'parentId': parentId},
      failureMessage: 'Failed to load work items',
    );
    return Swimlane(
      parentId: parentId,
      title: parent['title'] as String,
      cards: cards,
      assignedToUserId: parent['assignedToUserId'] as String?,
    );
  }

  Future<List<Map<String, dynamic>>> _loadChildren(String? parentId) =>
      _api.get(
        '/work-items',
        (json) => JsonApiClient.listOf(json, (item) => item),
        query: parentId == null ? null : {'parentId': parentId},
        failureMessage: 'Failed to load work items',
      );
}
