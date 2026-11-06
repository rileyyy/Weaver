import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/work_item_detail/data/space_move_repository.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/space_move_choices.dart';
import 'package:weaver/features/work_item_detail/models/space_move_plan.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/shared/data/space_repository.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// The plan and the move are its own requests; the lookups are the detail
/// dialog's, so they share its caches.
@LazySingleton(as: SpaceMoveRepository)
class ApiSpaceMoveRepository implements SpaceMoveRepository {
  ApiSpaceMoveRepository(
    http.Client client,
    ApiEndpoint endpoint,
    this._details,
    this._spaces,
  ) : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;
  final WorkItemDetailRepository _details;
  final SpaceRepository _spaces;

  @override
  Future<List<Space>> loadSpaces() => _spaces.loadSpaces();

  @override
  Future<List<WorkItemStatus>> loadStatuses(String spaceId) =>
      _details.loadStatuses(spaceId);

  @override
  Future<List<WorkItemType>> loadTypes(String spaceId) =>
      _details.loadTypes(spaceId);

  @override
  Future<List<WorkItemPriority>> loadPriorities(String spaceId) =>
      _details.loadPriorities(spaceId);

  @override
  Future<List<WorkItem>> searchWorkItems(String spaceId, String text) =>
      _details.searchWorkItems(spaceId, text);

  @override
  Future<WorkItem> getItem(String id) => _details.getItem(id);

  @override
  Future<SpaceMovePlan> planMove(String id, String targetSpaceId) => _api.get(
    '/work-items/$id/space-move-plan',
    (json) => SpaceMovePlan.fromJson(json as Map<String, dynamic>),
    query: {'targetSpaceId': targetSpaceId},
    failureMessage: 'Failed to work out the move',
  );

  @override
  Future<WorkItem> moveToSpace(
    String id,
    SpaceMoveChoices choices, {
    required int expectedVersion,
  }) => _api.post(
    '/work-items/$id/space-move',
    {
      'targetSpaceId': choices.targetSpaceId,
      'parentId': choices.parentId,
      'statusMap': choices.statuses,
      'typeMap': choices.types,
      'priorityMap': choices.priorities,
      'expectedVersion': expectedVersion,
    },
    (json) => WorkItem.fromJson(
      (json as Map<String, dynamic>)['item'] as Map<String, dynamic>,
    ),
    failureMessage: 'Failed to move the work item',
  );
}
