import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/data/api_status_repository.dart';
import 'package:weaver/shared/data/api_user_directory_repository.dart';

const _baseUrl = 'http://backend.test/api';

void main() {
  group('ApiStatusRepository', () {
    test('fetches statuses once and shares the result', () async {
      var calls = 0;
      final client = MockClient((_) async {
        calls++;
        return http.Response(
          jsonEncode([
            {'id': 's1', 'name': 'To Do', 'order': 0, 'color': '#1E88E5'},
          ]),
          200,
        );
      });
      final repository = ApiStatusRepository(client, _baseUrl);

      final results = await Future.wait([
        repository.loadStatuses(),
        repository.loadStatuses(),
      ]);
      await repository.loadStatuses();

      expect(calls, 1);
      expect(results.first.single.name, 'To Do');
    });

    test('does not cache a failure', () async {
      var fail = true;
      final client = MockClient(
        (_) async =>
            fail ? http.Response('', 500) : http.Response(jsonEncode([]), 200),
      );
      final repository = ApiStatusRepository(client, _baseUrl);

      await expectLater(
        repository.loadStatuses(),
        throwsA(isA<ApiException>()),
      );
      fail = false;

      expect(await repository.loadStatuses(), isEmpty);
    });
  });

  group('ApiUserDirectoryRepository', () {
    late int calls;
    late DateTime now;
    late ApiUserDirectoryRepository repository;

    setUp(() {
      calls = 0;
      now = DateTime(2026, 9, 26, 12);
      final client = MockClient((_) async {
        calls++;
        return http.Response(
          jsonEncode([
            {'id': 'u1', 'username': 'alice', 'kind': 'Human'},
          ]),
          200,
        );
      });
      repository = ApiUserDirectoryRepository(client, _baseUrl, now: () => now);
    });

    test('reuses the list while it is fresh', () async {
      await repository.loadUsers();
      now = now.add(const Duration(minutes: 4));
      final users = await repository.loadUsers();

      expect(calls, 1);
      expect(users.single.username, 'alice');
    });

    test('fetches again once the list is older than maxAge', () async {
      await repository.loadUsers();
      now = now.add(ApiUserDirectoryRepository.maxAge);
      await repository.loadUsers();

      expect(calls, 2);
    });
  });
}
