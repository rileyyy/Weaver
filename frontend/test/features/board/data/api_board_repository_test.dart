import 'dart:convert';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/api_board_repository.dart';
import 'package:weaver/shared/data/api_status_repository.dart';
import 'package:weaver/shared/data/api_user_directory_repository.dart';

http.Response _jsonResponse(Object body, {int statusCode = 200}) =>
    http.Response(
      jsonEncode(body),
      statusCode,
      headers: {'content-type': 'application/json'},
    );

void main() {
  const baseUrl = 'http://backend.test/api';

  test("loadRootScopeItemId returns the first board's scope item", () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/boards') {
        return _jsonResponse([
          {'id': 'board-1', 'name': 'Main', 'scopeItemId': 'epic-1'},
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    expect(await repository.loadRootScopeItemId(), 'epic-1');
  });

  test('loadRootScopeItemId returns null when no boards exist', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/boards') return _jsonResponse([]);
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    expect(await repository.loadRootScopeItemId(), isNull);
  });

  /// Serves /statuses and /work-items/swimlanes for scope `epic-1` with
  /// one lane (`lane-1`) holding one card with the given extra fields.
  MockClient boardClient({
    Map<String, dynamic> laneFields = const {},
    Map<String, dynamic> cardFields = const {},
    List<Map<String, dynamic>> statuses = const [],
    List<String>? requestLog,
  }) => MockClient((request) async {
    requestLog?.add(request.url.toString());
    if (request.url.path == '/api/statuses') return _jsonResponse(statuses);
    if (request.url.path == '/api/work-items/swimlanes' &&
        request.url.queryParameters['scopeItemId'] == 'epic-1') {
      return _jsonResponse([
        {
          'lane': {
            'id': 'lane-1',
            'number': 10,
            'title': 'Lane One',
            'parentId': 'epic-1',
            'statusId': 'status-todo',
            ...laneFields,
          },
          'cards': [
            {
              'id': 'card-1',
              'number': 1,
              'title': 'Card One',
              'parentId': 'lane-1',
              'statusId': 'status-todo',
              ...cardFields,
            },
          ],
        },
      ]);
    }
    throw StateError('Unexpected request: ${request.url}');
  });

  ApiBoardRepository repositoryFor(http.Client client) => ApiBoardRepository(
    client,
    baseUrl,
    ApiStatusRepository(client, baseUrl),
    ApiUserDirectoryRepository(client, baseUrl),
  );

  test('loadBoard builds swimlanes from one swimlanes request', () async {
    final requests = <String>[];
    final client = boardClient(
      statuses: [
        {'id': 'status-todo', 'name': 'To Do', 'order': 0, 'color': '#1E88E5'},
      ],
      requestLog: requests,
    );

    final board = await repositoryFor(client).loadBoard('epic-1');

    expect(board.statuses.single.id, 'status-todo');
    expect(board.statuses.single.color, const Color(0xFF1E88E5));
    expect(board.swimlanes.single.parentId, 'lane-1');
    expect(board.swimlanes.single.title, 'Lane One');
    expect(board.swimlanes.single.cards.single.id, 'card-1');
    expect(board.swimlanes.single.cards.single.number, 1);
    expect(requests, [
      '$baseUrl/statuses',
      '$baseUrl/work-items/swimlanes?scopeItemId=epic-1',
    ]);
  });

  test('loadBoard treats a null scope as top-level', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/statuses') return _jsonResponse([]);
      if (request.url.path == '/api/work-items/swimlanes') {
        expect(request.url.queryParameters, isEmpty);
        return _jsonResponse([]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final board = await repositoryFor(client).loadBoard(null);

    expect(board.swimlanes, isEmpty);
  });

  test("loadBoard parses each card's start and end date", () async {
    final client = boardClient(
      cardFields: {'startDate': '2026-01-10', 'endDate': null},
    );

    final board = await repositoryFor(client).loadBoard('epic-1');

    final card = board.swimlanes.single.cards.single;
    expect(card.startDate, DateTime(2026, 1, 10));
    expect(card.endDate, isNull);
  });

  test("loadBoard parses each card's description", () async {
    final client = boardClient(cardFields: {'description': 'Some detail'});

    final board = await repositoryFor(client).loadBoard('epic-1');

    expect(board.swimlanes.single.cards.single.description, 'Some detail');
  });

  test("loadBoard parses each card's tags", () async {
    final client = boardClient(
      cardFields: {
        'tags': ['urgent', 'needs review'],
      },
    );

    final board = await repositoryFor(client).loadBoard('epic-1');

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

      final board = await repositoryFor(client).loadBoard('epic-1');

      expect(board.swimlanes.single.assignedToUserId, 'user-lane');
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

      final repository = ApiBoardRepository(
        client,
        baseUrl,
        ApiStatusRepository(client, baseUrl),
        ApiUserDirectoryRepository(client, baseUrl),
      );

      await expectLater(
        repository.loadBoard(null),
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

  test('changeStatus posts the new status and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    await repository.changeStatus('card-1', 'status-done');

    expect(sentRequest, isNotNull);
    expect(sentRequest!.url.path, '/api/work-items/card-1/status');
    expect(jsonDecode(sentRequest!.body), {'statusId': 'status-done'});
  });

  test('changeStatus throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Conflict.'}, statusCode: 409);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    await expectLater(
      repository.changeStatus('card-1', 'status-done'),
      throwsA(isA<ApiException>()),
    );
  });

  test('reparentItem posts the new parent and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    await repository.reparentItem('card-1', 'lane-2');

    expect(sentRequest, isNotNull);
    expect(sentRequest!.url.path, '/api/work-items/card-1/parent');
    expect(jsonDecode(sentRequest!.body), {'parentId': 'lane-2'});
  });

  test('reparentItem throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Cycle detected.'}, statusCode: 409);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    await expectLater(
      repository.reparentItem('card-1', 'lane-2'),
      throwsA(isA<ApiException>()),
    );
  });

  test('loadUsers parses the user directory', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/users') {
        return _jsonResponse([
          {'id': 'user-1', 'username': 'riley', 'kind': 'Human'},
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    final users = await repository.loadUsers();

    expect(users.single.id, 'user-1');
    expect(users.single.username, 'riley');
  });

  test(
    'loadAllItems parses the flat item list, including top-level items',
    () async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/work-items/all') {
          return _jsonResponse([
            {
              'id': 'root-1',
              'number': 1,
              'title': 'Root',
              'parentId': null,
              'statusId': 'status-todo',
              'assignedToUserId': null,
            },
            {
              'id': 'child-1',
              'number': 2,
              'title': 'Child',
              'parentId': 'root-1',
              'statusId': 'status-todo',
              'assignedToUserId': 'user-1',
              'tags': ['urgent'],
            },
          ]);
        }
        throw StateError('Unexpected request: ${request.url}');
      });

      final repository = ApiBoardRepository(
        client,
        baseUrl,
        ApiStatusRepository(client, baseUrl),
        ApiUserDirectoryRepository(client, baseUrl),
      );
      final items = await repository.loadAllItems();

      expect(items.map((i) => i.id), ['root-1', 'child-1']);
      expect(items.first.number, 1);
      expect(items.first.parentId, isNull);
      expect(items.first.assignedToUserId, isNull);
      expect(items.first.tags, isEmpty);
      expect(items.last.parentId, 'root-1');
      expect(items.last.assignedToUserId, 'user-1');
      expect(items.last.tags, ['urgent']);
    },
  );

  test('loadAllItems throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Something went wrong.',
      }, statusCode: 500);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    await expectLater(repository.loadAllItems(), throwsA(isA<ApiException>()));
  });

  test('rescheduleItem posts the new dates and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });
    // A picked day is local midnight; it must be sent as that same calendar
    // day, not shifted by converting to UTC.
    final start = DateTime(2026, 2, 1);

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    await repository.rescheduleItem('card-1', start, null);

    expect(sentRequest, isNotNull);
    expect(sentRequest!.url.path, '/api/work-items/card-1/schedule');
    expect(jsonDecode(sentRequest!.body), {
      'startDate': '2026-02-01',
      'endDate': null,
    });
  });

  test('rescheduleItem throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Start must be before end.',
      }, statusCode: 400);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    await expectLater(
      repository.rescheduleItem('card-1', DateTime.utc(2026, 2, 1), null),
      throwsA(isA<ApiException>()),
    );
  });

  test('assign posts the new assignee and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    await repository.assign('card-1', 'user-1');

    expect(sentRequest, isNotNull);
    expect(sentRequest!.method, 'POST');
    expect(sentRequest!.url.path, '/api/work-items/card-1/assignee');
    expect(jsonDecode(sentRequest!.body), {'userId': 'user-1'});
  });

  test('assign posts a null userId to clear the assignee', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    await repository.assign('card-1', null);

    expect(jsonDecode(sentRequest!.body), {'userId': null});
  });

  test('assign throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'User not found.'}, statusCode: 404);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    await expectLater(
      repository.assign('card-1', 'missing-user'),
      throwsA(isA<ApiException>()),
    );
  });

  test('setTags posts the new tag list and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );
    await repository.setTags('card-1', ['urgent', 'needs review']);

    expect(sentRequest, isNotNull);
    expect(sentRequest!.method, 'POST');
    expect(sentRequest!.url.path, '/api/work-items/card-1/tags');
    expect(jsonDecode(sentRequest!.body), {
      'tags': ['urgent', 'needs review'],
    });
  });

  test('setTags throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Tags must not be blank.',
      }, statusCode: 400);
    });

    final repository = ApiBoardRepository(
      client,
      baseUrl,
      ApiStatusRepository(client, baseUrl),
      ApiUserDirectoryRepository(client, baseUrl),
    );

    await expectLater(
      repository.setTags('card-1', ['']),
      throwsA(isA<ApiException>()),
    );
  });
}
