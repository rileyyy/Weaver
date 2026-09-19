import 'dart:convert';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/api_board_repository.dart';

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

    final repository = ApiBoardRepository(client, baseUrl);

    expect(await repository.loadRootScopeItemId(), 'epic-1');
  });

  test('loadRootScopeItemId returns null when no boards exist', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/boards') return _jsonResponse([]);
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(client, baseUrl);

    expect(await repository.loadRootScopeItemId(), isNull);
  });

  test('loadBoard builds swimlanes from the given scope and its children', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/statuses') {
        return _jsonResponse([
          {'id': 'status-todo', 'name': 'To Do', 'order': 0, 'color': '#1E88E5'},
        ]);
      }
      if (request.url.path == '/api/work-items' &&
          request.url.queryParameters['parentId'] == 'epic-1') {
        return _jsonResponse([
          {
            'id': 'lane-1',
            'title': 'Lane One',
            'parentId': 'epic-1',
            'statusId': 'status-todo',
          },
        ]);
      }
      if (request.url.path == '/api/work-items' &&
          request.url.queryParameters['parentId'] == 'lane-1') {
        return _jsonResponse([
          {
            'id': 'card-1',
            'title': 'Card One',
            'parentId': 'lane-1',
            'statusId': 'status-todo',
          },
        ]);
      }

      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(client, baseUrl);
    final board = await repository.loadBoard('epic-1');

    expect(board.statuses.single.id, 'status-todo');
    expect(board.statuses.single.color, const Color(0xFF1E88E5));
    expect(board.swimlanes.single.parentId, 'lane-1');
    expect(board.swimlanes.single.title, 'Lane One');
    expect(board.swimlanes.single.cards.single.id, 'card-1');
  });

  test('loadBoard treats a null scope as top-level', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/statuses') return _jsonResponse([]);
      if (request.url.path == '/api/work-items') {
        expect(request.url.queryParameters, isEmpty);
        return _jsonResponse([]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(client, baseUrl);
    final board = await repository.loadBoard(null);

    expect(board.swimlanes, isEmpty);
  });

  test('loadBoard throws an ApiException with the problem detail on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Something went wrong.',
      }, statusCode: 500);
    });

    final repository = ApiBoardRepository(client, baseUrl);

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
  });

  test('changeStatus posts the new status and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });

    final repository = ApiBoardRepository(client, baseUrl);
    await repository.changeStatus('card-1', 'status-done');

    expect(sentRequest, isNotNull);
    expect(sentRequest!.url.path, '/api/work-items/card-1/status');
    expect(jsonDecode(sentRequest!.body), {'statusId': 'status-done'});
  });

  test('changeStatus throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Conflict.'}, statusCode: 409);
    });

    final repository = ApiBoardRepository(client, baseUrl);

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

    final repository = ApiBoardRepository(client, baseUrl);
    await repository.reparentItem('card-1', 'lane-2');

    expect(sentRequest, isNotNull);
    expect(sentRequest!.url.path, '/api/work-items/card-1/parent');
    expect(jsonDecode(sentRequest!.body), {'parentId': 'lane-2'});
  });

  test('reparentItem throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Cycle detected.'}, statusCode: 409);
    });

    final repository = ApiBoardRepository(client, baseUrl);

    await expectLater(
      repository.reparentItem('card-1', 'lane-2'),
      throwsA(isA<ApiException>()),
    );
  });

  test("loadBoard parses each card's start and end date", () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/statuses') return _jsonResponse([]);
      if (request.url.path == '/api/work-items' &&
          request.url.queryParameters['parentId'] == 'epic-1') {
        return _jsonResponse([
          {
            'id': 'lane-1',
            'title': 'Lane One',
            'parentId': 'epic-1',
            'statusId': 'status-todo',
          },
        ]);
      }
      if (request.url.path == '/api/work-items' &&
          request.url.queryParameters['parentId'] == 'lane-1') {
        return _jsonResponse([
          {
            'id': 'card-1',
            'title': 'Card One',
            'parentId': 'lane-1',
            'statusId': 'status-todo',
            'startDate': '2026-01-10T00:00:00Z',
            'endDate': null,
          },
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(client, baseUrl);
    final board = await repository.loadBoard('epic-1');

    final card = board.swimlanes.single.cards.single;
    expect(card.startDate, DateTime.parse('2026-01-10T00:00:00Z'));
    expect(card.endDate, isNull);
  });

  test("loadBoard parses each card's description", () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/statuses') return _jsonResponse([]);
      if (request.url.path == '/api/work-items' &&
          request.url.queryParameters['parentId'] == 'epic-1') {
        return _jsonResponse([
          {
            'id': 'lane-1',
            'title': 'Lane One',
            'parentId': 'epic-1',
            'statusId': 'status-todo',
          },
        ]);
      }
      if (request.url.path == '/api/work-items' &&
          request.url.queryParameters['parentId'] == 'lane-1') {
        return _jsonResponse([
          {
            'id': 'card-1',
            'title': 'Card One',
            'parentId': 'lane-1',
            'statusId': 'status-todo',
            'description': 'Some detail',
          },
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(client, baseUrl);
    final board = await repository.loadBoard('epic-1');

    expect(board.swimlanes.single.cards.single.description, 'Some detail');
  });

  test('loadAllItems parses the flat item list, including top-level items', () async {
    final client = MockClient((request) async {
      if (request.url.path == '/api/work-items/all') {
        return _jsonResponse([
          {
            'id': 'root-1',
            'title': 'Root',
            'parentId': null,
            'statusId': 'status-todo',
          },
          {
            'id': 'child-1',
            'title': 'Child',
            'parentId': 'root-1',
            'statusId': 'status-todo',
          },
        ]);
      }
      throw StateError('Unexpected request: ${request.url}');
    });

    final repository = ApiBoardRepository(client, baseUrl);
    final items = await repository.loadAllItems();

    expect(items.map((i) => i.id), ['root-1', 'child-1']);
    expect(items.first.parentId, isNull);
    expect(items.last.parentId, 'root-1');
  });

  test('loadAllItems throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({'detail': 'Something went wrong.'}, statusCode: 500);
    });

    final repository = ApiBoardRepository(client, baseUrl);

    await expectLater(repository.loadAllItems(), throwsA(isA<ApiException>()));
  });

  test('rescheduleItem posts the new dates and succeeds on 200', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 200);
    });
    final start = DateTime.utc(2026, 2, 1);

    final repository = ApiBoardRepository(client, baseUrl);
    await repository.rescheduleItem('card-1', start, null);

    expect(sentRequest, isNotNull);
    expect(sentRequest!.url.path, '/api/work-items/card-1/schedule');
    expect(jsonDecode(sentRequest!.body), {
      'startDate': start.toIso8601String(),
      'endDate': null,
    });
  });

  test('rescheduleItem throws an ApiException on failure', () async {
    final client = MockClient((request) async {
      return _jsonResponse({
        'detail': 'Start must be before end.',
      }, statusCode: 400);
    });

    final repository = ApiBoardRepository(client, baseUrl);

    await expectLater(
      repository.rescheduleItem('card-1', DateTime.utc(2026, 2, 1), null),
      throwsA(isA<ApiException>()),
    );
  });
}
