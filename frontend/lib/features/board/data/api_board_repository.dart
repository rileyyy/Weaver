import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_dates.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/shared/data/space_repository.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/data/type_repository.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// Items per `/work-items/search` request; the server allows up to 1000.
const int searchPageSize = 500;

typedef _RankedItem = ({double rank, WorkItem item});

/// Board data backed by the REST API. Swimlanes are the direct children of
/// whichever scope item [loadBoard] is asked for (or the Space's top-level
/// items); each swimlane's cards are that swimlane's own direct children.
@LazySingleton(as: BoardRepository)
class ApiBoardRepository implements BoardRepository {
  ApiBoardRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._statuses,
    this._types,
    this._users,
    this._spaces,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final StatusRepository _statuses;
  final TypeRepository _types;
  final UserDirectoryRepository _users;
  final SpaceRepository _spaces;

  static WorkItem _item(Object? json) =>
      WorkItem.fromJson(json as Map<String, dynamic>);

  /// Every write answers with the item and the fields it changed; the board
  /// only needs the item, which carries the new version.
  static WorkItem _writtenItem(Object? json) =>
      _item((json as Map<String, dynamic>)['item']);

  @override
  Future<List<Space>> loadSpaces() => _spaces.loadSpaces();

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) async {
    // Three requests at most (a Space's statuses and Types are cached after
    // their first load): /swimlanes returns every lane with its cards, instead of
    // one request per lane.
    final statuses = await _statuses.loadStatuses(spaceId);
    final types = await _types.loadTypes(spaceId);
    final swimlanes = await _api.get(
      '/work-items/swimlanes',
      (json) => JsonApiClient.listOf(json, _toSwimlane),
      query: {'spaceId': spaceId, 'scopeItemId': ?scopeItemId},
      failureMessage: 'Failed to load the board',
    );
    return BoardData(statuses: statuses, swimlanes: swimlanes, types: types);
  }

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) =>
      queryItems(WorkItemQuery(spaceIds: {spaceId}));

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) => _api.get(
    '/custom-fields',
    (json) => JsonApiClient.listOf(json, CustomField.fromJson),
    query: {'spaceId': spaceId},
    failureMessage: "Failed to load the Space's fields",
  );

  /// Pages by the server's cursor, which unlike an offset over Rank can't
  /// skip or repeat items that move mid-load (ADR-0006). The cursor walks
  /// Number order, so Rank order is restored afterwards: a view left on
  /// manual sort shows siblings in the order it's given.
  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) async {
    final parameters = _searchParameters(query);
    final ranked = <_RankedItem>[];
    String? cursor;
    do {
      final page = await _api.get(
        '/work-items/search',
        _toSearchPage,
        query: {...parameters, 'cursor': ?cursor},
        failureMessage: 'Failed to load work items',
      );
      ranked.addAll(page.items);
      cursor = page.nextCursor;
    } while (cursor != null);

    ranked.sort(_byRankThenNumber);
    return [for (final entry in ranked) entry.item];
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
    required String spaceId,
    required String? parentId,
    required String statusId,
  }) => _api.postIgnoringBody('/work-items', {
    'title': title,
    'description': description,
    'parentId': parentId,
    'statusId': statusId,
    // A child joins its parent's Space, so only a top-level item names one.
    if (parentId == null) 'spaceId': spaceId,
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

  /// Leaves out every input that filters nothing, so an unfiltered query
  /// asks only for its Spaces.
  static Map<String, Object> _searchParameters(WorkItemQuery query) => {
    if (query.spaceIds.isNotEmpty) 'spaceIds': query.spaceIds.toList(),
    if (query.text.isNotEmpty) 'text': query.text,
    if (query.tags.isNotEmpty) 'tags': query.tags.toList(),
    // Any is the server's default.
    if (query.tagMatch == TagMatch.all) 'tagMatch': 'All',
    'from': ?formatCalendarDate(query.from),
    'to': ?formatCalendarDate(query.to),
    if (query.applyDoneHiding) 'applyDoneHiding': 'true',
    if (query.backlogOnly) 'backlogOnly': 'true',
    if (query.scheduledOrInProgressOnly) 'scheduledOrInProgressOnly': 'true',
    'limit': '$searchPageSize',
  };

  static ({List<_RankedItem> items, String? nextCursor}) _toSearchPage(
    Object? json,
  ) {
    final page = json as Map<String, dynamic>;
    return (
      items: JsonApiClient.listOf(
        page['items'],
        (item) => (
          rank: (item['rank'] as num).toDouble(),
          item: WorkItem.fromJson(item),
        ),
      ),
      nextCursor: page['nextCursor'] as String?,
    );
  }

  static int _byRankThenNumber(_RankedItem a, _RankedItem b) {
    final byRank = a.rank.compareTo(b.rank);
    return byRank != 0 ? byRank : a.item.number.compareTo(b.item.number);
  }

  static SwimlaneItems _toSwimlane(Map<String, dynamic> json) => SwimlaneItems(
    lane: _item(json['lane']),
    cards: JsonApiClient.listOf(json['cards'], WorkItem.fromJson),
  );
}
