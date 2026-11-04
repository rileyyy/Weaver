import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/api_work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/quick_pick.dart';
import 'package:weaver/features/work_item_detail/models/work_item_link_type.dart';
import 'package:weaver/shared/data/api_status_repository.dart';
import 'package:weaver/shared/data/api_type_repository.dart';
import 'package:weaver/shared/data/api_user_directory_repository.dart';
import 'package:weaver/shared/data/api_work_item_search_repository.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

http.Response _jsonResponse(Object body, {int statusCode = 200}) =>
    http.Response(
      jsonEncode(body),
      statusCode,
      headers: {'content-type': 'application/json'},
    );

Map<String, dynamic> _itemJson({
  String? typeId,
  String? priorityId,
  String? assignedToUserId,
  List<String> tags = const [],
  Map<String, Object?> fields = const {},
  int version = 7,
}) => {
  'id': 'item-1',
  'number': 1,
  'spaceId': 'space-home',
  'parentId': null,
  'title': 'A task',
  'description': null,
  'statusId': 'status-todo',
  'typeId': typeId,
  'priorityId': priorityId,
  'assignedToUserId': assignedToUserId,
  'tags': tags,
  'fields': fields,
  'rank': 0,
  'startDate': null,
  'endDate': null,
  'createdAtUtc': '2026-01-01T00:00:00Z',
  'updatedAtUtc': '2026-01-01T00:00:00Z',
  'version': version,
};

/// The body every work-item write answers with.
Map<String, dynamic> _writeJson(
  Map<String, dynamic> item, {
  List<String> changedFields = const [],
}) => {'item': item, 'changedFields': changedFields};

Map<String, dynamic> _commentJson({DateTime? updatedAt}) => {
  'id': 'comment-1',
  'workItemId': 'item-1',
  'authorUserId': 'user-1',
  'authorUsername': 'alice',
  'body': 'Looks good',
  'createdAtUtc': '2026-01-01T00:00:00Z',
  'updatedAtUtc': updatedAt?.toIso8601String(),
};

Map<String, dynamic> _linkJson({String type = 'Related'}) => {
  'id': 'link-1',
  'type': type,
  'linkedWorkItemId': 'item-2',
  'linkedWorkItemNumber': 2,
  'linkedWorkItemTitle': 'Other task',
  'linkedWorkItemSpaceId': 'space-work',
};

const _endpoint = FixedApiEndpoint('http://backend.test/api');

ApiWorkItemDetailRepository _repository(http.Client client) =>
    ApiWorkItemDetailRepository(
      client,
      _endpoint,
      ApiStatusRepository(client, _endpoint),
      ApiTypeRepository(client, _endpoint),
      ApiUserDirectoryRepository(client, _endpoint),
      ApiWorkItemSearchRepository(client, _endpoint),
    );

void main() {
  test(
    'getItem parses the work item, including type/priority/assignee',
    () async {
      final client = MockClient(
        (request) async => _jsonResponse(
          _itemJson(
            typeId: 'type-1',
            priorityId: 'priority-urgent',
            assignedToUserId: 'user-1',
          ),
        ),
      );
      final repository = _repository(client);

      final item = await repository.getItem('item-1');

      expect(item.typeId, 'type-1');
      expect(item.priorityId, 'priority-urgent');
      expect(item.assignedToUserId, 'user-1');
    },
  );

  test('getItem parses tags', () async {
    final client = MockClient(
      (request) async =>
          _jsonResponse(_itemJson(tags: ['urgent', 'needs review'])),
    );
    final repository = _repository(client);

    final item = await repository.getItem('item-1');

    expect(item.tags, ['urgent', 'needs review']);
  });

  test('loadChildren queries by parentId and parses the child list', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([
        {
          'id': 'child-1',
          'number': 7,
          'title': 'A sub-item',
          'statusId': 'status-todo',
        },
      ]);
    });
    final repository = _repository(client);

    final children = await repository.loadChildren('item-1');

    expect(sentRequest!.url.path, '/api/work-items');
    expect(sentRequest!.url.queryParameters['parentId'], 'item-1');
    expect(children.single.title, 'A sub-item');
    expect(children.single.number, 7);
  });

  test("loadTypes parses the Space's type list", () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([
        {'id': 'type-1', 'name': 'Project', 'order': 0},
      ]);
    });
    final repository = _repository(client);

    final types = await repository.loadTypes('home');

    expect(sentRequest!.url.queryParameters['spaceId'], 'home');
    expect(types.single.name, 'Project');
  });

  test('loadPriorities parses the priority list', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([
        {'id': 'priority-low', 'name': 'Low', 'order': 0},
        {'id': 'priority-high', 'name': 'High', 'order': 2},
      ]);
    });
    final repository = _repository(client);

    final priorities = await repository.loadPriorities('home');

    expect(sentRequest!.url.path, '/api/work-item-priorities');
    expect(sentRequest!.url.queryParameters['spaceId'], 'home');
    expect(priorities.map((p) => p.name), ['Low', 'High']);
    expect(priorities.last.id, 'priority-high');
    expect(priorities.last.order, 2);
  });

  test('loadUsers parses the user list', () async {
    final client = MockClient(
      (request) async => _jsonResponse([
        {
          'id': 'user-1',
          'username': 'alice',
          'kind': 'Human',
          'isAdmin': false,
        },
      ]),
    );
    final repository = _repository(client);

    final users = await repository.loadUsers();

    expect(users.single.username, 'alice');
  });

  test(
    'updateDetails patches the new fields and parses the response',
    () async {
      http.Request? sentRequest;
      final client = MockClient((request) async {
        sentRequest = request;
        return _jsonResponse(
          _writeJson(_itemJson(typeId: 'type-1', priorityId: 'priority-high')),
        );
      });
      final repository = _repository(client);

      final item = await repository.updateDetails(
        'item-1',
        title: 'New title',
        description: 'New description',
        typeId: 'type-1',
        priorityId: 'priority-high',
        expectedVersion: 7,
      );

      expect(item.typeId, 'type-1');
      expect(sentRequest!.method, 'PATCH');
      expect(sentRequest!.url.path, '/api/work-items/item-1/details');
      expect(jsonDecode(sentRequest!.body), {
        'title': 'New title',
        'description': 'New description',
        'typeId': 'type-1',
        'priorityId': 'priority-high',
        'expectedVersion': 7,
      });
    },
  );

  test(
    'updateDescription patches only the description and the version',
    () async {
      http.Request? sentRequest;
      final client = MockClient((request) async {
        sentRequest = request;
        return _jsonResponse(_writeJson(_itemJson(version: 8)));
      });
      final repository = _repository(client);

      final item = await repository.updateDescription(
        'item-1',
        '- [x] milk',
        expectedVersion: 7,
      );

      expect(item.version, 8);
      expect(sentRequest!.method, 'PATCH');
      expect(sentRequest!.url.path, '/api/work-items/item-1/details');
      expect(jsonDecode(sentRequest!.body), {
        'description': '- [x] milk',
        'expectedVersion': 7,
      });
    },
  );

  test('updateDetails returns the new version from the response', () async {
    final client = MockClient(
      (request) async => _jsonResponse(_writeJson(_itemJson(version: 8))),
    );
    final repository = _repository(client);

    final item = await repository.updateDetails(
      'item-1',
      title: 'New title',
      description: null,
      typeId: null,
      priorityId: null,
      expectedVersion: 7,
    );

    expect(item.version, 8);
  });

  test(
    'a 409 from a save throws a conflict ApiException with the server detail',
    () async {
      final client = MockClient(
        (request) async => _jsonResponse({
          'status': 409,
          'detail': 'Changed by someone else.',
        }, statusCode: 409),
      );
      final repository = _repository(client);

      final call = repository.updateTags('item-1', [
        'urgent',
      ], expectedVersion: 7);

      await expectLater(
        call,
        throwsA(
          isA<ApiException>()
              .having((e) => e.isConflict, 'isConflict', isTrue)
              .having(
                (e) => e.message,
                'message',
                contains('Changed by someone else.'),
              ),
        ),
      );
    },
  );

  test(
    'assign puts the userId and expected version to the assignee endpoint',
    () async {
      http.Request? sentRequest;
      final client = MockClient((request) async {
        sentRequest = request;
        return _jsonResponse(_writeJson(_itemJson(assignedToUserId: 'user-1')));
      });
      final repository = _repository(client);

      final item = await repository.assign(
        'item-1',
        'user-1',
        expectedVersion: 7,
      );

      expect(item.assignedToUserId, 'user-1');
      expect(sentRequest!.method, 'PUT');
      expect(sentRequest!.url.path, '/api/work-items/item-1/assignee');
      expect(jsonDecode(sentRequest!.body), {
        'userId': 'user-1',
        'expectedVersion': 7,
      });
    },
  );

  test('updateTags patches the new tag list to the tags endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_writeJson(_itemJson(tags: ['urgent'])));
    });
    final repository = _repository(client);

    final item = await repository.updateTags('item-1', [
      'urgent',
    ], expectedVersion: 7);

    expect(sentRequest!.method, 'PATCH');
    expect(sentRequest!.url.path, '/api/work-items/item-1/tags');
    expect(jsonDecode(sentRequest!.body), {
      'tags': ['urgent'],
      'expectedVersion': 7,
    });
    expect(item.tags, ['urgent']);
  });

  test("loadCustomFields parses the Type's fields and their options", () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([
        {
          'id': 'field-floor',
          'typeId': 'type-room',
          'name': 'Floor',
          'kind': 'SingleSelect',
          'order': 0,
          'options': [
            {'id': 'option-oak', 'name': 'Oak'},
          ],
        },
      ]);
    });
    final repository = _repository(client);

    final fields = await repository.loadCustomFields('type-room');

    expect(sentRequest!.url.path, '/api/custom-fields');
    expect(sentRequest!.url.queryParameters['typeId'], 'type-room');
    expect(fields.single.kind, CustomFieldKind.singleSelect);
    expect(fields.single.optionName('option-oak'), 'Oak');
  });

  test('setCustomFields patches the values by field id', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(
        _writeJson(_itemJson(fields: {'field-budget': 1200}, version: 8)),
      );
    });
    final repository = _repository(client);

    final item = await repository.setCustomFields('item-1', {
      'field-budget': 1200,
      'field-due': null,
    }, expectedVersion: 7);

    expect(sentRequest!.method, 'PATCH');
    expect(sentRequest!.url.path, '/api/work-items/item-1/fields');
    expect(jsonDecode(sentRequest!.body), {
      'values': {'field-budget': 1200, 'field-due': null},
      'expectedVersion': 7,
    });
    expect(item.fields, {'field-budget': 1200});
  });

  test('reschedule patches both dates', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_writeJson(_itemJson()));
    });
    final repository = _repository(client);
    final start = DateTime(2026, 2, 1);

    await repository.reschedule('item-1', start, null, expectedVersion: 7);

    expect(sentRequest!.method, 'PATCH');
    expect(sentRequest!.url.path, '/api/work-items/item-1/schedule');
    expect(jsonDecode(sentRequest!.body), {
      'startDate': '2026-02-01',
      'endDate': null,
      'expectedVersion': 7,
    });
  });

  test('updateWhen patches only the When and the version', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(
        _writeJson({
          ..._itemJson(version: 8),
          'when': 'after drywall',
          'whenResolved': false,
        }),
      );
    });
    final repository = _repository(client);

    final item = await repository.updateWhen(
      'item-1',
      'after drywall',
      expectedVersion: 7,
    );

    expect(item.when, 'after drywall');
    expect(item.whenResolved, isFalse);
    expect(sentRequest!.method, 'PATCH');
    expect(sentRequest!.url.path, '/api/work-items/item-1/details');
    expect(jsonDecode(sentRequest!.body), {
      'when': 'after drywall',
      'expectedVersion': 7,
    });
  });

  test('loadQuickPicks parses each pick with its dates', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([
        {
          'pick': 'NextWeek',
          'startDate': '2026-11-09',
          'endDate': '2026-11-15',
        },
      ]);
    });
    final repository = _repository(client);

    final picks = await repository.loadQuickPicks();

    expect(sentRequest!.url.path, '/api/quick-picks');
    expect(picks.single.pick, QuickPick.nextWeek);
    expect(picks.single.startDate, DateTime(2026, 11, 9));
    expect(picks.single.endDate, DateTime(2026, 11, 15));
  });

  test('loadComments parses the comment list', () async {
    final client = MockClient(
      (request) async => _jsonResponse([_commentJson()]),
    );
    final repository = _repository(client);

    final comments = await repository.loadComments('item-1');

    expect(comments.single.authorUsername, 'alice');
    expect(comments.single.body, 'Looks good');
  });

  test('addComment posts the body to the comments endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_commentJson());
    });
    final repository = _repository(client);

    await repository.addComment('item-1', 'Looks good');

    expect(sentRequest!.method, 'POST');
    expect(sentRequest!.url.path, '/api/work-items/item-1/comments');
    expect(jsonDecode(sentRequest!.body), {'body': 'Looks good'});
  });

  test('updateComment puts the new body to the comment endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_commentJson(updatedAt: DateTime.utc(2026, 1, 2)));
    });
    final repository = _repository(client);

    final comment = await repository.updateComment('comment-1', 'Edited');

    expect(sentRequest!.method, 'PUT');
    expect(sentRequest!.url.path, '/api/comments/comment-1');
    expect(comment.updatedAt, isNotNull);
  });

  test('deleteComment deletes the comment endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 204);
    });
    final repository = _repository(client);

    await repository.deleteComment('comment-1');

    expect(sentRequest!.method, 'DELETE');
    expect(sentRequest!.url.path, '/api/comments/comment-1');
  });

  test(
    'deleteItem deletes the work item with the cascade flag and version',
    () async {
      http.Request? sentRequest;
      final client = MockClient((request) async {
        sentRequest = request;
        return http.Response('', 204);
      });
      final repository = _repository(client);

      await repository.deleteItem('item-1', cascade: true, expectedVersion: 7);

      expect(sentRequest!.method, 'DELETE');
      expect(sentRequest!.url.path, '/api/work-items/item-1');
      expect(sentRequest!.url.queryParameters, {
        'cascade': 'true',
        'expectedVersion': '7',
      });
    },
  );

  test('loadLinks parses the typed link list', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([_linkJson(type: 'BlockedBy')]);
    });
    final repository = _repository(client);

    final links = await repository.loadLinks('item-1');

    expect(sentRequest!.url.path, '/api/work-items/item-1/links');
    expect(links.single.type, WorkItemLinkType.blockedBy);
    expect(links.single.linkedWorkItemNumber, 2);
    expect(links.single.linkedWorkItemTitle, 'Other task');
  });

  test('addLink posts the target and the type from this side', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_linkJson(type: 'Blocks'), statusCode: 201);
    });
    final repository = _repository(client);

    final link = await repository.addLink(
      'item-1',
      'item-2',
      WorkItemLinkType.blocks,
    );

    expect(sentRequest!.method, 'POST');
    expect(sentRequest!.url.path, '/api/work-items/item-1/links');
    expect(jsonDecode(sentRequest!.body), {
      'targetWorkItemId': 'item-2',
      'type': 'Blocks',
    });
    expect(link.type, WorkItemLinkType.blocks);
  });

  test("addLink reports the server's reason for refusing a link", () async {
    final client = MockClient(
      (request) async => _jsonResponse({
        'title': 'Conflict',
        'detail': 'These items already block each other.',
      }, statusCode: 409),
    );
    final repository = _repository(client);

    await expectLater(
      repository.addLink('item-1', 'item-2', WorkItemLinkType.blocks),
      throwsA(
        isA<ApiException>()
            .having((e) => e.isConflict, 'isConflict', isTrue)
            .having(
              (e) => e.message,
              'message',
              contains('already block each other'),
            ),
      ),
    );
  });

  test('loadUrlLinks parses the URL link list', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse([
        {'id': 'url-1', 'url': 'https://example.com/pr/7', 'label': 'PR 7'},
        {'id': 'url-2', 'url': 'https://example.com/quote', 'label': null},
      ]);
    });
    final repository = _repository(client);

    final links = await repository.loadUrlLinks('item-1');

    expect(sentRequest!.url.path, '/api/work-items/item-1/url-links');
    expect(links.map((l) => l.displayName), [
      'PR 7',
      'https://example.com/quote',
    ]);
  });

  test('addUrlLink posts the URL and label', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse({
        'id': 'url-1',
        'url': 'https://example.com/pr/7',
        'label': 'PR 7',
      }, statusCode: 201);
    });
    final repository = _repository(client);

    final link = await repository.addUrlLink(
      'item-1',
      'https://example.com/pr/7',
      label: 'PR 7',
    );

    expect(sentRequest!.method, 'POST');
    expect(sentRequest!.url.path, '/api/work-items/item-1/url-links');
    expect(jsonDecode(sentRequest!.body), {
      'url': 'https://example.com/pr/7',
      'label': 'PR 7',
    });
    expect(link.id, 'url-1');
  });

  test('addUrlLink leaves the label out when there is none', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse({
        'id': 'url-1',
        'url': 'https://example.com',
        'label': null,
      }, statusCode: 201);
    });
    final repository = _repository(client);

    await repository.addUrlLink('item-1', 'https://example.com');

    expect(jsonDecode(sentRequest!.body), {'url': 'https://example.com'});
  });

  test('deleteUrlLink deletes the work-item-url-links endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 204);
    });
    final repository = _repository(client);

    await repository.deleteUrlLink('url-1');

    expect(sentRequest!.method, 'DELETE');
    expect(sentRequest!.url.path, '/api/work-item-url-links/url-1');
  });

  test('deleteLink deletes the work-item-links endpoint', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return http.Response('', 204);
    });
    final repository = _repository(client);

    await repository.deleteLink('link-1');

    expect(sentRequest!.method, 'DELETE');
    expect(sentRequest!.url.path, '/api/work-item-links/link-1');
  });

  test('changeStatus puts the status and version, ranking it first', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse(_writeJson(_itemJson(version: 8)));
    });

    final saved = await _repository(
      client,
    ).changeStatus('item-1', 'status-done', expectedVersion: 7);

    expect(sentRequest!.method, 'PUT');
    expect(sentRequest!.url.path, '/api/work-items/item-1/status');
    expect(jsonDecode(sentRequest!.body), {
      'statusId': 'status-done',
      'afterId': null,
      'expectedVersion': 7,
    });
    expect(saved.version, 8);
  });

  test('reparent puts the new parent, or null for none', () async {
    final bodies = <Object?>[];
    final client = MockClient((request) async {
      expect(request.url.path, '/api/work-items/item-1/parent');
      bodies.add(jsonDecode(request.body));
      return _jsonResponse(_writeJson(_itemJson()));
    });
    final repository = _repository(client);

    await repository.reparent('item-1', 'parent-1', expectedVersion: 7);
    await repository.reparent('item-1', null, expectedVersion: 8);

    expect(bodies, [
      {'parentId': 'parent-1', 'afterId': null, 'expectedVersion': 7},
      {'parentId': null, 'afterId': null, 'expectedVersion': 8},
    ]);
  });

  test('createSubItem posts a child of the parent and returns it', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse({
        ..._itemJson(),
        'id': 'child-1',
        'number': 12,
        'title': 'Paint',
      }, statusCode: 201);
    });

    final child = await _repository(
      client,
    ).createSubItem('item-1', title: 'Paint', statusId: 'status-todo');

    expect(sentRequest!.method, 'POST');
    expect(sentRequest!.url.path, '/api/work-items');
    expect(jsonDecode(sentRequest!.body), {
      'title': 'Paint',
      'description': null,
      'parentId': 'item-1',
      'statusId': 'status-todo',
    });
    expect(child.id, 'child-1');
    expect(child.number, 12);
    expect(child.title, 'Paint');
  });

  test('searchWorkItems searches the given Space', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse({
        'items': [_itemJson()],
        'nextCursor': null,
      });
    });

    final items = await _repository(client).searchWorkItems('space-home', '#1');

    expect(sentRequest!.url.path, '/api/work-items/search');
    expect(sentRequest!.url.queryParameters['spaceIds'], 'space-home');
    expect(sentRequest!.url.queryParameters['text'], '#1');
    expect(items.single.id, 'item-1');
  });

  test('searchAllWorkItems searches every Space', () async {
    http.Request? sentRequest;
    final client = MockClient((request) async {
      sentRequest = request;
      return _jsonResponse({
        'items': [_itemJson()],
        'nextCursor': null,
      });
    });

    final items = await _repository(client).searchAllWorkItems('#1');

    expect(sentRequest!.url.path, '/api/work-items/search');
    expect(sentRequest!.url.queryParameters.containsKey('spaceIds'), isFalse);
    expect(sentRequest!.url.queryParameters['text'], '#1');
    expect(items.single.id, 'item-1');
  });
}
