import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/api_lookup_admin_repository.dart';
import 'package:weaver/features/admin/data/api_status_admin_repository.dart';
import 'package:weaver/features/admin/data/api_user_admin_repository.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/new_user.dart';
import 'package:weaver/features/admin/models/status_category.dart';
import 'package:weaver/features/admin/models/status_details.dart';
import 'package:weaver/shared/data/status_repository.dart';
import 'package:weaver/shared/data/user_directory_repository.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_status.dart';

const _baseUrl = 'http://backend.test/api';

class _CountingStatusCache implements StatusRepository {
  int invalidations = 0;

  @override
  Future<List<WorkItemStatus>> loadStatuses() async => const [];

  @override
  void invalidate() => invalidations++;
}

class _CountingDirectory implements UserDirectoryRepository {
  int invalidations = 0;

  @override
  Future<List<User>> loadUsers() async => const [];

  @override
  void invalidate() => invalidations++;
}

Map<String, dynamic> _statusJson(String id) => {
  'id': id,
  'name': 'Review',
  'order': 0,
  'category': 'Doing',
  'color': '#00897B',
};

Map<String, dynamic> _userJson(String id) => {
  'id': id,
  'username': 'bob',
  'kind': 'Human',
  'isAdmin': false,
  'isActive': true,
  'lockedUntilUtc': null,
  'createdAtUtc': '2026-09-01T12:00:00Z',
};

void main() {
  group('ApiStatusAdminRepository', () {
    late List<http.Request> requests;
    late int responseStatus;
    late _CountingStatusCache cache;
    late ApiStatusAdminRepository repository;

    setUp(() {
      requests = [];
      responseStatus = 200;
      cache = _CountingStatusCache();
      final client = MockClient((request) async {
        requests.add(request);
        if (responseStatus != 200) return http.Response('', responseStatus);
        if (request.method == 'DELETE') return http.Response('', 204);
        final isList = request.url.path.endsWith('/order');
        return http.Response(
          jsonEncode(isList ? [_statusJson('s1')] : _statusJson('s1')),
          200,
        );
      });
      repository = ApiStatusAdminRepository(client, _baseUrl, cache);
    });

    test('create posts the details and drops the status cache', () async {
      await repository.create(
        const StatusDetails(
          name: 'Review',
          category: StatusCategory.doing,
          color: '#00897B',
        ),
      );

      expect(requests.single.method, 'POST');
      expect(requests.single.url.path, '/api/statuses');
      expect(jsonDecode(requests.single.body), {
        'name': 'Review',
        'category': 'Doing',
        'color': '#00897B',
      });
      expect(cache.invalidations, 1);
    });

    test('reorder puts every id in order', () async {
      await repository.reorder(['s2', 's1']);

      expect(requests.single.method, 'PUT');
      expect(requests.single.url.path, '/api/statuses/order');
      expect(jsonDecode(requests.single.body), {
        'orderedIds': ['s2', 's1'],
      });
    });

    test('delete sends the replacement', () async {
      await repository.delete('s1', replacementId: 's2');

      expect(
        requests.single.url.toString(),
        '$_baseUrl/statuses/s1?replacementId=s2',
      );
      expect(cache.invalidations, 1);
    });

    test('a failed write leaves the cache alone', () async {
      responseStatus = 409;

      await expectLater(
        repository.delete('s1', replacementId: 's2'),
        throwsA(isA<ApiException>()),
      );
      expect(cache.invalidations, 0);
    });
  });

  group('ApiLookupAdminRepository', () {
    late List<http.Request> requests;
    late ApiLookupAdminRepository repository;

    setUp(() {
      requests = [];
      final client = MockClient((request) async {
        requests.add(request);
        if (request.method == 'DELETE') return http.Response('', 204);
        return http.Response(
          jsonEncode({'id': 'p1', 'name': 'Critical', 'order': 4}),
          200,
        );
      });
      repository = ApiLookupAdminRepository(client, _baseUrl);
    });

    test('uses the path of the kind', () async {
      await repository.create(LookupKind.layers, 'Epic');
      await repository.rename(LookupKind.priorities, 'p1', 'Critical');

      expect(requests[0].url.path, '/api/work-item-layers');
      expect(requests[1].url.path, '/api/work-item-priorities/p1');
      expect(jsonDecode(requests[1].body), {'name': 'Critical'});
    });

    test('delete without a replacement sends no query', () async {
      await repository.delete(LookupKind.priorities, 'p1');

      expect(
        requests.single.url.toString(),
        '$_baseUrl/work-item-priorities/p1',
      );
    });

    test('delete with a replacement sends it', () async {
      await repository.delete(LookupKind.priorities, 'p1', replacementId: 'p2');

      expect(requests.single.url.queryParameters, {'replacementId': 'p2'});
    });
  });

  group('ApiUserAdminRepository', () {
    late List<http.Request> requests;
    late _CountingDirectory directory;
    late ApiUserAdminRepository repository;

    setUp(() {
      requests = [];
      directory = _CountingDirectory();
      final client = MockClient((request) async {
        requests.add(request);
        if (request.method == 'DELETE' &&
            !request.url.path.endsWith('/lockout')) {
          return http.Response('', 204);
        }
        return http.Response(jsonEncode(_userJson('u1')), 200);
      });
      repository = ApiUserAdminRepository(client, _baseUrl, directory);
    });

    test('create posts the new account and drops the directory', () async {
      await repository.create(
        const NewUser(
          username: 'bob',
          password: 'a long password',
          isAdmin: true,
        ),
      );

      expect(requests.single.url.path, '/api/admin/users');
      expect(jsonDecode(requests.single.body), {
        'username': 'bob',
        'password': 'a long password',
        'isAdmin': true,
      });
      expect(directory.invalidations, 1);
    });

    test('each field change puts to its own sub-resource', () async {
      await repository.rename('u1', 'rob');
      await repository.resetPassword('u1', 'a long password');
      await repository.setAdmin('u1', isAdmin: true);
      await repository.setActive('u1', isActive: false);

      expect(requests.map((r) => '${r.method} ${r.url.path}'), [
        'PUT /api/admin/users/u1/username',
        'PUT /api/admin/users/u1/password',
        'PUT /api/admin/users/u1/admin',
        'PUT /api/admin/users/u1/active',
      ]);
      expect(jsonDecode(requests[3].body), {'isActive': false});
      expect(directory.invalidations, 4);
    });

    test('unlock deletes the lockout and returns the user', () async {
      final user = await repository.unlock('u1');

      expect(requests.single.method, 'DELETE');
      expect(requests.single.url.path, '/api/admin/users/u1/lockout');
      expect(user.username, 'bob');
    });

    test('delete removes the account', () async {
      await repository.delete('u1');

      expect(requests.single.method, 'DELETE');
      expect(requests.single.url.path, '/api/admin/users/u1');
      expect(directory.invalidations, 1);
    });
  });
}
