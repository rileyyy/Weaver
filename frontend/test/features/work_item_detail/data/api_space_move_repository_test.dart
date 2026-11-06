import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/features/work_item_detail/data/api_space_move_repository.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/space_move_choices.dart';
import 'package:weaver/shared/data/space_repository.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

const _endpoint = FixedApiEndpoint('http://backend.test/api');

/// The lookups the repository passes through aren't what these tests are
/// about.
class _Unused implements WorkItemDetailRepository, SpaceRepository {
  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

ApiSpaceMoveRepository _repository(http.Client client) =>
    ApiSpaceMoveRepository(client, _endpoint, _Unused(), _Unused());

http.Response _json(Object body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);

Map<String, dynamic> _itemJson() => {
  'id': 'item-1',
  'number': 1,
  'spaceId': 'workshop',
  'parentId': 'bench',
  'title': 'Shed',
  'description': null,
  'statusId': 'quoted',
  'typeId': null,
  'priorityId': null,
  'assignedToUserId': null,
  'tags': <String>[],
  'fields': <String, Object?>{},
  'rank': 0,
  'startDate': null,
  'endDate': null,
  'createdAtUtc': '2026-01-01T00:00:00Z',
  'updatedAtUtc': '2026-01-01T00:00:00Z',
  'version': 8,
};

void main() {
  test('planMove reads the plan for the target Space', () async {
    final client = MockClient((request) async {
      expect(request.url.path, '/api/work-items/item-1/space-move-plan');
      expect(request.url.queryParameters, {'targetSpaceId': 'workshop'});
      return _json({
        'workItemId': 'item-1',
        'targetSpaceId': 'workshop',
        'itemCount': 3,
        'trashedItemCount': 1,
        'clearedFieldValueCount': 2,
        'statuses': [
          {
            'sourceId': 'todo',
            'sourceName': 'To Do',
            'targetId': 'quoted',
            'targetName': 'Quoted',
            'itemCount': 3,
          },
        ],
        'types': [
          {
            'sourceId': 'room',
            'sourceName': 'Room',
            'targetId': null,
            'targetName': null,
            'itemCount': 1,
          },
        ],
        'priorities': <Object>[],
      });
    });

    final plan = await _repository(client).planMove('item-1', 'workshop');

    expect(plan.itemCount, 3);
    expect(plan.trashedItemCount, 1);
    expect(plan.clearedFieldValueCount, 2);
    expect(plan.statuses.single.sourceName, 'To Do');
    expect(plan.statuses.single.targetId, 'quoted');
    expect(plan.types.single.targetId, isNull);
    expect(plan.priorities, isEmpty);
  });

  test('moveToSpace posts the target, parent, maps and version', () async {
    Object? sent;
    final client = MockClient((request) async {
      expect(request.method, 'POST');
      expect(request.url.path, '/api/work-items/item-1/space-move');
      sent = jsonDecode(request.body);
      return _json({
        'item': _itemJson(),
        'changedFields': ['spaceId'],
      });
    });

    final moved = await _repository(client).moveToSpace(
      'item-1',
      const SpaceMoveChoices(
        targetSpaceId: 'workshop',
        parentId: 'bench',
        statuses: {'todo': 'quoted'},
        types: {'room': null},
        priorities: {},
      ),
      expectedVersion: 7,
    );

    expect(sent, {
      'targetSpaceId': 'workshop',
      'parentId': 'bench',
      'statusMap': {'todo': 'quoted'},
      'typeMap': {'room': null},
      'priorityMap': <String, Object?>{},
      'expectedVersion': 7,
    });
    expect(moved.spaceId, 'workshop');
    expect(moved.version, 8);
  });
}
