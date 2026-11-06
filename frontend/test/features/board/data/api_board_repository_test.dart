import 'dart:convert';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/api_board_repository.dart';
import 'package:weaver/shared/data/api_space_repository.dart';
import 'package:weaver/shared/data/api_status_repository.dart';
import 'package:weaver/shared/data/api_type_repository.dart';
import 'package:weaver/shared/data/api_user_directory_repository.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/work_item_query.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

http.Response _jsonResponse(Object body, {int statusCode = 200}) =>
    http.Response(
      jsonEncode(body),
      statusCode,
      headers: {'content-type': 'application/json'},
    );

/// A `WorkItemDto` as the server sends it, with [fields] overriding the
/// defaults.
Map<String, dynamic> _itemJson(
  String id, {
  int number = 1,
  String? parentId,
  Map<String, dynamic> fields = const {},
}) => {
  'id': id,
  'number': number,
  'spaceId': 'space-home',
  'title': id,
  'parentId': parentId,
  'statusId': 'status-todo',
  'rank': number.toDouble(),
  'tags': <String>[],
  'createdAtUtc': '2026-01-01T00:00:00Z',
  'updatedAtUtc': '2026-01-01T00:00:00Z',
  'version': 1,
  ...fields,
};

/// A `WorkItemPageDto`: one page of `/work-items/search`.
Map<String, dynamic> _pageJson(
  List<Map<String, dynamic>> items, {
  String? nextCursor,
}) => {'items': items, 'nextCursor': nextCursor};

/// The body every work-item write answers with.
Map<String, dynamic> _writeJson(Map<String, dynamic> item) => {
  'item': item,
  'changedFields': <String>[],
};

void main() {
  const baseUrl = 'http://backend.test/api';
  const endpoint = FixedApiEndpoint(baseUrl);

  ApiBoardRepository repositoryFor(http.Client client) => ApiBoardRepository(
    client,
    endpoint,
    ApiStatusRepository(client, endpoint),
    ApiTypeRepository(client, endpoint),
    ApiUserDirectoryRepository(client, endpoint),
    ApiSpaceRepository(client, endpoint),
  );

  /// Answers every request with [item] as a write response, recording the
  /// request in [sent].
  MockClient writeClient(
    List<http.Request> sent, {
    Map<String, dynamic>? item,
  }) => MockClient((request) async {
    sent.add(request);
    return _jsonResponse(_writeJson(item ?? _itemJson('card-1')));
  });

  test('loadSpaces parses every Space', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/spaces') {
        return _jsonResponse([
          {'id': 'space-home', 'name': 'Home'},
          {'id': 'space-village', 'name': 'Village'},
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    expect(await repositoryFor(client).loadSpaces(), const [
      Space(id: 'space-home', name: 'Home'),
      Space(id: 'space-village', name: 'Village'),
    ]);
  });

  test("loadCustomFields asks for every Type's fields in the Space", () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/custom-fields' &&
          request.url.queryParameters['spaceId'] == 'space-home') {
        return _jsonResponse([
          {
            'id': 'field-hours',
            'typeId': 'type-task',
            'name': 'Hours',
            'kind': 'Number',
            'order': 0,
            'options': <Object>[],
            'rollsUp': true,
          },
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final fields = await repositoryFor(client).loadCustomFields('space-home');

    expect(fields.single.name, 'Hours');
    expect(fields.single.rollsUp, isTrue);
  });

  group('createWorkItem', () {
    Future<Map<String, dynamic>> sentBody({required String? parentId}) async {
      late Map<String, dynamic> body;
      final client = MockClient((request) async {
        body = jsonDecode(request.body) as Map<String, dynamic>;
        return _jsonResponse(_itemJson('new-item'), statusCode: 201);
      });
      await repositoryFor(client).createWorkItem(
        title: 'Fix the fence',
        spaceId: 'space-village',
        parentId: parentId,
        statusId: 'status-todo',
      );
      return body;
    }

    test('names the Space for a top-level item', () async {
      final body = await sentBody(parentId: null);

      expect(body['spaceId'], 'space-village');
      expect(body['parentId'], isNull);
    });

    test(
      "leaves the Space out for a child, which joins its parent's",
      () async {
        final body = await sentBody(parentId: 'lane-1');

        expect(body.containsKey('spaceId'), isFalse);
        expect(body['parentId'], 'lane-1');
      },
    );
  });

  /// Serves /statuses and /work-items/swimlanes for scope `epic-1` with
  /// one lane (`lane-1`) holding one card with the given extra fields.
  MockClient boardClient({
    Map<String, dynamic> laneFields = const {},
    Map<String, dynamic> cardFields = const {},
    List<Map<String, dynamic>> statuses = const [],
    List<Map<String, dynamic>> types = const [],
    List<String>? requestLog,
  }) => MockClient((request) async {
    requestLog?.add(request.url.toString());
    if (request.url.path == '/api/statuses') return _jsonResponse(statuses);
    if (request.url.path == '/api/work-item-types') {
      return _jsonResponse(types);
    }
    if (request.url.path == '/api/work-items/swimlanes' &&
        request.url.queryParameters['spaceId'] == 'space-home' &&
        request.url.queryParameters['scopeItemId'] == 'epic-1') {
      return _jsonResponse([
        {
          'lane': _itemJson(
            'lane-1',
            number: 10,
            parentId: 'epic-1',
            fields: {'title': 'Lane One', ...laneFields},
          ),
          'cards': [
            _itemJson(
              'card-1',
              parentId: 'lane-1',
              fields: {'title': 'Card One', ...cardFields},
            ),
          ],
        },
      ]);
    }
    throw StateError('Unexpected request: ${request.url}');
  });

  test('loadBoard builds swimlanes from one swimlanes request', () async {
    final requests = <String>[];
    final client = boardClient(
      statuses: [
        {'id': 'status-todo', 'name': 'To Do', 'order': 0, 'color': '#1E88E5'},
      ],
      requestLog: requests,
    );

    final board = await repositoryFor(client).loadBoard('space-home', 'epic-1');

    expect(board.statuses.single.id, 'status-todo');
    expect(board.statuses.single.color, const Color(0xFF1E88E5));
    expect(board.swimlanes.single.lane.id, 'lane-1');
    expect(board.swimlanes.single.lane.title, 'Lane One');
    expect(board.swimlanes.single.cards.single.id, 'card-1');
    expect(board.swimlanes.single.cards.single.number, 1);
    expect(requests, [
      '$baseUrl/statuses?spaceId=space-home',
      '$baseUrl/work-item-types?spaceId=space-home',
      '$baseUrl/work-items/swimlanes?spaceId=space-home&scopeItemId=epic-1',
    ]);
  });

  test('loadBoard keeps the version of each lane and card', () async {
    final client = boardClient(
      laneFields: {'version': 41},
      cardFields: {'version': 42},
    );

    final board = await repositoryFor(client).loadBoard('space-home', 'epic-1');

    expect(board.swimlanes.single.lane.version, 41);
    expect(board.swimlanes.single.cards.single.version, 42);
  });

  test('loadBoard loads the types and each card keeps its type', () async {
    final client = boardClient(
      types: [
        {'id': 'type-task', 'name': 'Task', 'order': 2, 'color': '#43A047'},
      ],
      cardFields: {'typeId': 'type-task'},
    );

    final board = await repositoryFor(client).loadBoard('space-home', 'epic-1');

    expect(board.types.single.color, const Color(0xFF43A047));
    expect(board.swimlanes.single.cards.single.typeId, 'type-task');
  });

  test("loadBoard treats a null scope as the Space's top level", () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/statuses') return _jsonResponse([]);
      if (request.url.path == '/api/work-item-types') return _jsonResponse([]);
      if (request.url.path == '/api/work-items/swimlanes') {
        expect(request.url.queryParameters, {'spaceId': 'space-home'});
        return _jsonResponse([]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final board = await repositoryFor(client).loadBoard('space-home', null);

    expect(board.swimlanes, isEmpty);
  });

  test("loadBoard parses each card's start and end date", () async {
    final client = boardClient(
      cardFields: {'startDate': '2026-01-10', 'endDate': null},
    );

    final board = await repositoryFor(client).loadBoard('space-home', 'epic-1');

    final card = board.swimlanes.single.cards.single;
    expect(card.startDate, DateTime(2026, 1, 10));
    expect(card.endDate, isNull);
  });

  test("loadBoard parses each card's description", () async {
    final client = boardClient(cardFields: {'description': 'Some detail'});

    final board = await repositoryFor(client).loadBoard('space-home', 'epic-1');

    expect(board.swimlanes.single.cards.single.description, 'Some detail');
  });

  test("loadBoard parses each card's tags", () async {
    final client = boardClient(
      cardFields: {
        'tags': ['urgent', 'needs review'],
      },
    );

    final board = await repositoryFor(client).loadBoard('space-home', 'epic-1');

    expect(board.swimlanes.single.cards.single.tags, [
      'urgent',
      'needs review',
    ]);
  });

  test(
    "loadBoard parses the assignee of both a lane's own item and its cards",
    () async {
      final client = boardClient(
        laneFields: {'assignedToUserId': 'user-lane'},
        cardFields: {'assignedToUserId': 'user-card'},
      );

      final board = await repositoryFor(
        client,
      ).loadBoard('space-home', 'epic-1');

      expect(board.swimlanes.single.lane.assignedToUserId, 'user-lane');
      expect(board.swimlanes.single.cards.single.assignedToUserId, 'user-card');
    },
  );

  test(
    'loadBoard throws an ApiException with the problem detail on failure',
    () async {
      final client = MockClient((request) async {
        return _jsonResponse({
          'detail': 'Something went wrong.',
        }, statusCode: 500);
      });

      await expectLater(
        repositoryFor(client).loadBoard('space-home', null),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            contains('Something went wrong.'),
          ),
        ),
      );
    },
  );

  test('getItem loads one work item', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/work-items/card-1') {
        return _jsonResponse(
          _itemJson('card-1', fields: {'title': 'Fresh', 'version': 8}),
        );
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final item = await repositoryFor(client).getItem('card-1');

    expect(item.title, 'Fresh');
    expect(item.version, 8);
  });

  test('changeStatus puts the new status, rank anchor and expected version, '
      'and returns the saved item', () async {
    final sent = <http.Request>[];
    final client = writeClient(
      sent,
      item: _itemJson(
        'card-1',
        fields: {'statusId': 'status-done', 'version': 6},
      ),
    );

    final saved = await repositoryFor(client).changeStatus(
      'card-1',
      'status-done',
      afterId: 'card-2',
      expectedVersion: 5,
    );

    expect(sent.single.method, 'PUT');
    expect(sent.single.url.path, '/api/work-items/card-1/status');
    expect(jsonDecode(sent.single.body), {
      'statusId': 'status-done',
      'afterId': 'card-2',
      'expectedVersion': 5,
    });
    expect(saved.statusId, 'status-done');
    expect(saved.version, 6);
  });

  test('changeStatus throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Conflict.'}, statusCode: 409);
    });

    await expectLater(
      repositoryFor(client).changeStatus(
        'card-1',
        'status-done',
        afterId: null,
        expectedVersion: 1,
      ),
      throwsA(
        isA<ApiException>().having((e) => e.isConflict, 'conflict', true),
      ),
    );
  });

  test('reparentItem puts the new parent and expected version', () async {
    final sent = <http.Request>[];
    final client = writeClient(
      sent,
      item: _itemJson('card-1', parentId: 'lane-2', fields: {'version': 3}),
    );

    final saved = await repositoryFor(
      client,
    ).reparentItem('card-1', 'lane-2', expectedVersion: 2);

    expect(sent.single.method, 'PUT');
    expect(sent.single.url.path, '/api/work-items/card-1/parent');
    expect(jsonDecode(sent.single.body), {
      'parentId': 'lane-2',
      'expectedVersion': 2,
    });
    expect(saved.parentId, 'lane-2');
    expect(saved.version, 3);
  });

  test('reparentItem throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Cycle detected.'}, statusCode: 409);
    });

    await expectLater(
      repositoryFor(
        client,
      ).reparentItem('card-1', 'lane-2', expectedVersion: 1),
      throwsA(isA<ApiException>()),
    );
  });

  test('loadUsers parses the user directory', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/users') {
        return _jsonResponse([
          {
            'id': 'user-1',
            'username': 'riley',
            'kind': 'Human',
            'isAdmin': false,
          },
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final users = await repositoryFor(client).loadUsers();

    expect(users.single.id, 'user-1');
    expect(users.single.username, 'riley');
  });

  test(
    'loadAllItems parses the flat item list, including top-level items',
    () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/work-items/search') {
          return _jsonResponse(
            _pageJson([
              _itemJson('root-1', fields: {'version': 4}),
              _itemJson(
                'child-1',
                number: 2,
                parentId: 'root-1',
                fields: {
                  'assignedToUserId': 'user-1',
                  'tags': ['urgent'],
                },
              ),
            ]),
          );
        }
        throw StateError('Unexpected request: ${request.url}');
      });

      final items = await repositoryFor(client).loadAllItems('space-home');

      expect(items.map((i) => i.id), ['root-1', 'child-1']);
      expect(items.first.number, 1);
      expect(items.first.version, 4);
      expect(items.first.parentId, isNull);
      expect(items.first.assignedToUserId, isNull);
      expect(items.first.tags, isEmpty);
      expect(items.last.parentId, 'root-1');
      expect(items.last.assignedToUserId, 'user-1');
      expect(items.last.tags, ['urgent']);
    },
  );

  test('loadAllItems asks the search for only the Space', () async {
    final sent = <Uri>[];
    final client = MockClient((request) async {
      sent.add(request.url);
      return _jsonResponse(_pageJson([]));
    });

    await repositoryFor(client).loadAllItems('space-home');

    expect(sent.single.path, '/api/work-items/search');
    expect(sent.single.queryParametersAll, {
      'spaceIds': ['space-home'],
      'limit': ['$searchPageSize'],
    });
  });

  test('loadAllItems follows the cursor until it runs out', () async {
    final cursors = <String?>[];
    final client = MockClient((request) async {
      final cursor = request.url.queryParameters['cursor'];
      cursors.add(cursor);
      return switch (cursor) {
        null => _jsonResponse(
          _pageJson([_itemJson('item-1')], nextCursor: 'after-1'),
        ),
        'after-1' => _jsonResponse(
          _pageJson([_itemJson('item-2', number: 2)], nextCursor: 'after-2'),
        ),
        _ => _jsonResponse(_pageJson([_itemJson('item-3', number: 3)])),
      };
    });

    final items = await repositoryFor(client).loadAllItems('space-home');

    expect(cursors, [null, 'after-1', 'after-2']);
    expect(items.map((i) => i.id), ['item-1', 'item-2', 'item-3']);
  });

  test('loadAllItems throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Something went wrong.',
      }, statusCode: 500);
    });

    await expectLater(
      repositoryFor(client).loadAllItems('space-home'),
      throwsA(isA<ApiException>()),
    );
  });

  group('queryItems', () {
    /// The query string [query] sends on its first (only) request.
    Future<Map<String, List<String>>> sentParameters(
      WorkItemQuery query,
    ) async {
      late Uri sent;
      final client = MockClient((request) async {
        sent = request.url;
        return _jsonResponse(_pageJson([]));
      });
      await repositoryFor(client).queryItems(query);
      expect(sent.path, '/api/work-items/search');
      return sent.queryParametersAll;
    }

    test('sends only the Space and page size when unfiltered', () async {
      final parameters = await sentParameters(
        WorkItemQuery(spaceId: 'space-home'),
      );

      expect(parameters, {
        'spaceIds': ['space-home'],
        'limit': ['$searchPageSize'],
      });
    });

    test('sends the text, trimmed', () async {
      final parameters = await sentParameters(
        WorkItemQuery(spaceId: 'space-home', text: '  fence post '),
      );

      expect(parameters['text'], ['fence post']);
    });

    test('leaves out blank text', () async {
      final parameters = await sentParameters(
        WorkItemQuery(spaceId: 'space-home', text: '   '),
      );

      expect(parameters.containsKey('text'), isFalse);
    });

    test('repeats the tags key once per tag', () async {
      final parameters = await sentParameters(
        WorkItemQuery(spaceId: 'space-home', tags: {'urgent', 'home'}),
      );

      expect(parameters['tags'], unorderedEquals(['urgent', 'home']));
    });

    test('leaves tagMatch out for Any, the server default', () async {
      final parameters = await sentParameters(
        WorkItemQuery(spaceId: 'space-home', tags: {'urgent', 'home'}),
      );

      expect(parameters.containsKey('tagMatch'), isFalse);
    });

    test('sends tagMatch=All for All with several tags', () async {
      final parameters = await sentParameters(
        WorkItemQuery(
          spaceId: 'space-home',
          tags: {'urgent', 'home'},
          tagMatch: TagMatch.all,
        ),
      );

      expect(parameters['tagMatch'], ['All']);
    });

    test('leaves tagMatch out for All with one tag', () async {
      final parameters = await sentParameters(
        WorkItemQuery(
          spaceId: 'space-home',
          tags: {'urgent'},
          tagMatch: TagMatch.all,
        ),
      );

      expect(parameters['tags'], ['urgent']);
      expect(parameters.containsKey('tagMatch'), isFalse);
    });

    test('sends the date window as calendar days', () async {
      // Local midnight, as the date picker gives it; converting to UTC
      // would send the previous day east of UTC.
      final parameters = await sentParameters(
        WorkItemQuery(
          spaceId: 'space-home',
          from: DateTime(2026, 3, 1),
          to: DateTime(2026, 3, 31),
        ),
      );

      expect(parameters['from'], ['2026-03-01']);
      expect(parameters['to'], ['2026-03-31']);
    });

    test('leaves out an open side of the date window', () async {
      final parameters = await sentParameters(
        WorkItemQuery(spaceId: 'space-home', to: DateTime(2026, 3, 31)),
      );

      expect(parameters.containsKey('from'), isFalse);
      expect(parameters['to'], ['2026-03-31']);
    });

    test("repeats the same query with each page's cursor", () async {
      final sent = <Map<String, List<String>>>[];
      final client = MockClient((request) async {
        sent.add(request.url.queryParametersAll);
        return request.url.queryParameters['cursor'] == null
            ? _jsonResponse(
                _pageJson([_itemJson('item-1')], nextCursor: 'after-1'),
              )
            : _jsonResponse(_pageJson([_itemJson('item-2', number: 2)]));
      });

      final items = await repositoryFor(
        client,
      ).queryItems(WorkItemQuery(spaceId: 'space-home', text: 'fence'));

      expect(items.map((i) => i.id), ['item-1', 'item-2']);
      expect(sent, hasLength(2));
      expect(sent.last, {
        ...sent.first,
        'cursor': ['after-1'],
      });
    });

    test('returns the items in Rank order, then Number', () async {
      final client = MockClient((request) async {
        return request.url.queryParameters['cursor'] == null
            ? _jsonResponse(
                _pageJson([
                  _itemJson('number-1', fields: {'rank': 3.0}),
                  _itemJson('number-2', number: 2, fields: {'rank': 1.0}),
                ], nextCursor: 'after-2'),
              )
            : _jsonResponse(
                _pageJson([
                  _itemJson('number-3', number: 3, fields: {'rank': 3.0}),
                  _itemJson('number-4', number: 4, fields: {'rank': 2.5}),
                ]),
              );
      });

      final items = await repositoryFor(
        client,
      ).queryItems(WorkItemQuery(spaceId: 'space-home'));

      expect(items.map((i) => i.id), [
        'number-2',
        'number-4',
        'number-1',
        'number-3',
      ]);
    });

    test('throws an ApiException with the status code on failure', () async {
      final client = MockClient((request) async {
        return _jsonResponse({
          'detail': "The date window can't end before it starts.",
        }, statusCode: 400);
      });

      await expectLater(
        repositoryFor(client).queryItems(
          WorkItemQuery(
            spaceId: 'space-home',
            from: DateTime(2026, 3, 31),
            to: DateTime(2026, 3, 1),
          ),
        ),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 400)
              .having(
                (e) => e.message,
                'message',
                contains("can't end before it starts"),
              ),
        ),
      );
    });
  });

  test('rescheduleItem patches the new dates and expected version', () async {
    final sent = <http.Request>[];
    final client = writeClient(
      sent,
      item: _itemJson('card-1', fields: {'startDate': '2026-02-01'}),
    );
    // A picked day is local midnight; it must be sent as that same calendar
    // day, not shifted by converting to UTC.
    final start = DateTime(2026, 2, 1);

    final saved = await repositoryFor(
      client,
    ).rescheduleItem('card-1', start, null, expectedVersion: 7);

    expect(sent.single.method, 'PATCH');
    expect(sent.single.url.path, '/api/work-items/card-1/schedule');
    expect(jsonDecode(sent.single.body), {
      'startDate': '2026-02-01',
      'endDate': null,
      'expectedVersion': 7,
    });
    expect(saved.startDate, start);
  });

  test('rescheduleItem throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Start must be before end.',
      }, statusCode: 400);
    });

    await expectLater(
      repositoryFor(client).rescheduleItem(
        'card-1',
        DateTime.utc(2026, 2, 1),
        null,
        expectedVersion: 1,
      ),
      throwsA(isA<ApiException>()),
    );
  });

  test('assign puts the new assignee and expected version', () async {
    final sent = <http.Request>[];
    final client = writeClient(
      sent,
      item: _itemJson('card-1', fields: {'assignedToUserId': 'user-1'}),
    );

    final saved = await repositoryFor(
      client,
    ).assign('card-1', 'user-1', expectedVersion: 3);

    expect(sent.single.method, 'PUT');
    expect(sent.single.url.path, '/api/work-items/card-1/assignee');
    expect(jsonDecode(sent.single.body), {
      'userId': 'user-1',
      'expectedVersion': 3,
    });
    expect(saved.assignedToUserId, 'user-1');
  });

  test('assign sends a null userId to clear the assignee', () async {
    final sent = <http.Request>[];
    final client = writeClient(sent);

    await repositoryFor(client).assign('card-1', null, expectedVersion: 3);

    expect(jsonDecode(sent.single.body), {
      'userId': null,
      'expectedVersion': 3,
    });
  });

  test('assign throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'User not found.'}, statusCode: 404);
    });

    await expectLater(
      repositoryFor(
        client,
      ).assign('card-1', 'missing-user', expectedVersion: 1),
      throwsA(isA<ApiException>()),
    );
  });

  test('setTags patches the new tag list and expected version', () async {
    final sent = <http.Request>[];
    final client = writeClient(
      sent,
      item: _itemJson(
        'card-1',
        fields: {
          'tags': ['urgent', 'needs review'],
        },
      ),
    );

    final saved = await repositoryFor(
      client,
    ).setTags('card-1', ['urgent', 'needs review'], expectedVersion: 4);

    expect(sent.single.method, 'PATCH');
    expect(sent.single.url.path, '/api/work-items/card-1/tags');
    expect(jsonDecode(sent.single.body), {
      'tags': ['urgent', 'needs review'],
      'expectedVersion': 4,
    });
    expect(saved.tags, ['urgent', 'needs review']);
  });

  test('setTags throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Tags must not be blank.',
      }, statusCode: 400);
    });

    await expectLater(
      repositoryFor(client).setTags('card-1', [''], expectedVersion: 1),
      throwsA(isA<ApiException>()),
    );
  });
}
