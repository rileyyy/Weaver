import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/shared/data/layer_repository.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';

/// Items per `/work-items/all` request; the server allows up to 1000.
const int allItemsPageSize = 500;

/// Board data backed by the REST API. Swimlanes are the direct children of
/// whichever scope item [loadBoard] is asked for (the first board's scope
/// item, or top-level items, by default); each swimlane's cards are that
/// swimlane's own direct children — mirroring `Board`'s doc comment on the
/// backend.
@LazySingleton(as: BoardRepository)
class ApiBoardRepository implements BoardRepository {
  ApiBoardRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._statuses,
    this._layers,
    this._users,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final StatusRepository _statuses;
  final LayerRepository _layers;
  final UserDirectoryRepository _users;

  static WorkItem _item(Object? json) =>
      WorkItem.fromJson(json as Map<String, dynamic>);

  /// Every write answers with the item and the fields it changed; the board
  /// only needs the item, which carries the new version.
  static WorkItem _writtenItem(Object? json) =>
      _item((json as Map<String, dynamic>)['item']);

  @override
  Future<String?> loadRootScopeItemId() => _api.get('/boards', (json) {
    final boards = json as List<dynamic>;
    if (boards.isEmpty) return null;
    return (boards.first as Map<String, dynamic>)['scopeItemId'] as String?;
  }, failureMessage: 'Failed to load boards');

  @override
  Future<BoardData> loadBoard(String? scopeItemId) async {
    // Three requests at most (statuses and layers are cached after the
    // first load): /swimlanes returns every lane with its cards, instead of
    // one request per lane.
    final statuses = await _statuses.loadStatuses();
    final layers = await _layers.loadLayers();
    final swimlanes = await _api.get(
      '/work-items/swimlanes',
      (json) => JsonApiClient.listOf(json, _toSwimlane),
      query: scopeItemId == null ? null : {'scopeItemId': scopeItemId},
      failureMessage: 'Failed to load the board',
    );
    return BoardData(statuses: statuses, swimlanes: swimlanes, layers: layers);
  }

  @override
  Future<List<WorkItem>> loadAllItems() async {
    final items = <WorkItem>[];
    while (true) {
      final page = await _api.get(
        '/work-items/all',
        (json) => JsonApiClient.listOf(json, WorkItem.fromJson),
        query: {'offset': '${items.length}', 'limit': '$allItemsPageSize'},
        failureMessage: 'Failed to load work items',
      );
      items.addAll(page);
      // The server caps each page, so a short page is the only end signal.
      if (page.length < allItemsPageSize) return items;
    }
  }

  @override
  Future<WorkItem> getItem(String id) => _api.get(
    '/work-items/$id',
    _item,
    failureMessage: 'Failed to load work item',
  );

  @override
  Future<List<User>> loadUsers() => _users.loadUsers();

  @override
  Future<WorkItem> changeStatus(
    String cardId,
    String newStatusId, {
    required String? afterId,
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$cardId/status',
    {
      'statusId': newStatusId,
      'afterId': afterId,
      'expectedVersion': expectedVersion,
    },
    _writtenItem,
    failureMessage: 'Failed to change status',
  );

  @override
  Future<WorkItem> reparentItem(
    String itemId,
    String newParentId, {
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$itemId/parent',
    {'parentId': newParentId, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to move item',
  );

  @override
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$itemId/schedule',
    {
      'startDate': formatCalendarDate(startDate),
      'endDate': formatCalendarDate(endDate),
      'expectedVersion': expectedVersion,
    },
    _writtenItem,
    failureMessage: 'Failed to reschedule item',
  );

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
  Future<WorkItem> assign(
    String workItemId,
    String? userId, {
    required int expectedVersion,
  }) => _api.put(
    '/work-items/$workItemId/assignee',
    {'userId': userId, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to assign work item',
  );

  @override
  Future<WorkItem> setTags(
    String workItemId,
    List<String> tags, {
    required int expectedVersion,
  }) => _api.patch(
    '/work-items/$workItemId/tags',
    {'tags': tags, 'expectedVersion': expectedVersion},
    _writtenItem,
    failureMessage: 'Failed to set tags',
  );

  static SwimlaneItems _toSwimlane(Map<String, dynamic> json) => SwimlaneItems(
    lane: _item(json['lane']),
    cards: JsonApiClient.listOf(json['cards'], WorkItem.fromJson),
  );
}
