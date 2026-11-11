import 'dart:convert';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/server_settings.dart';
import 'package:weaver/shared/data/api_status_repository.dart';
import 'package:weaver/shared/data/api_type_repository.dart';
import 'package:weaver/shared/data/api_user_directory_repository.dart';

import '../../core/network/fakes/fixed_api_endpoint.dart';
import '../../core/network/fakes/in_memory_server_origin_store.dart';

const _baseUrl = 'http://backend.test/api';
const _endpoint = FixedApiEndpoint(_baseUrl);

ServerSettings _switchableServer() => ServerSettings(
  InMemoryServerOriginStore(),
  buildTimeOrigin: 'https://first.example',
  isWeb: false,
);

void main() {
  group('ApiTypeRepository', () {
    test("fetches each Space's types separately, once each", () async {
      final requested = <String?>[];
      final client = MockClient((request) async {
        final spaceId = request.url.queryParameters['spaceId'];
        requested.add(spaceId);
        return http.Response(
          jsonEncode([
            {'id': 't-$spaceId', 'name': 'In $spaceId', 'order': 0},
          ]),
          200,
        );
      });
      final repository = ApiTypeRepository(client, _endpoint);

      final home = await repository.loadTypes('home');
      final software = await repository.loadTypes('software');
      await repository.loadTypes('home');

      expect(requested, ['home', 'software']);
      expect(home.single.name, 'In home');
      expect(software.single.name, 'In software');
    });

    test('fetches types once, with their colours', () async {
      var calls = 0;
      final client = MockClient((_) async {
        calls++;
        return http.Response(
          jsonEncode([
            {'id': 'l1', 'name': 'Task', 'order': 0, 'color': '#43A047'},
          ]),
          200,
        );
      });
      final repository = ApiTypeRepository(client, _endpoint);

      await repository.loadTypes('home');
      final types = await repository.loadTypes('home');

      expect(calls, 1);
      expect(types.single.color, const Color(0xFF43A047));
    });

    test('fetches again after invalidate', () async {
      var calls = 0;
      final client = MockClient((_) async {
        calls++;
        return http.Response(jsonEncode([]), 200);
      });
      final repository = ApiTypeRepository(client, _endpoint);

      await repository.loadTypes('home');
      repository.invalidate();
      await repository.loadTypes('home');

      expect(calls, 2);
    });

    test('does not cache a failure', () async {
      var fail = true;
      final client = MockClient(
        (_) async =>
            fail ? http.Response('', 500) : http.Response(jsonEncode([]), 200),
      );
      final repository = ApiTypeRepository(client, _endpoint);

      await expectLater(
        repository.loadTypes('home'),
        throwsA(isA<ApiException>()),
      );
      fail = false;

      expect(await repository.loadTypes('home'), isEmpty);
    });
  });

  group('ApiStatusRepository', () {
    test("fetches each Space's statuses separately, once each", () async {
      final requested = <String?>[];
      final client = MockClient((request) async {
        final spaceId = request.url.queryParameters['spaceId'];
        requested.add(spaceId);
        return http.Response(
          jsonEncode([
            {'id': 's-$spaceId', 'name': 'In $spaceId', 'order': 0},
          ]),
          200,
        );
      });
      final repository = ApiStatusRepository(client, _endpoint);

      final home = await repository.loadStatuses('home');
      final software = await repository.loadStatuses('software');
      await repository.loadStatuses('home');

      expect(requested, ['home', 'software']);
      expect(home.single.name, 'In home');
      expect(software.single.name, 'In software');
    });

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
      final repository = ApiStatusRepository(client, _endpoint);

      final results = await Future.wait([
        repository.loadStatuses('home'),
        repository.loadStatuses('home'),
      ]);
      await repository.loadStatuses('home');

      expect(calls, 1);
      expect(results.first.single.name, 'To Do');
    });

    test('fetches again after invalidate', () async {
      var calls = 0;
      final client = MockClient((_) async {
        calls++;
        return http.Response(jsonEncode([]), 200);
      });
      final repository = ApiStatusRepository(client, _endpoint);

      await repository.loadStatuses('home');
      repository.invalidate();
      await repository.loadStatuses('home');

      expect(calls, 2);
    });

    test('fetches again from a newly chosen server', () async {
      final hosts = <String>[];
      final client = MockClient((request) async {
        hosts.add(request.url.host);
        return http.Response(jsonEncode([]), 200);
      });
      final server = _switchableServer();
      final repository = ApiStatusRepository(client, server);

      await repository.loadStatuses('home');
      server.use('https://second.example');
      await repository.loadStatuses('home');

      expect(hosts, ['first.example', 'second.example']);
    });

    test('does not cache a failure', () async {
      var fail = true;
      final client = MockClient(
        (_) async =>
            fail ? http.Response('', 500) : http.Response(jsonEncode([]), 200),
      );
      final repository = ApiStatusRepository(client, _endpoint);

      await expectLater(
        repository.loadStatuses('home'),
        throwsA(isA<ApiException>()),
      );
      fail = false;

      expect(await repository.loadStatuses('home'), isEmpty);
    });
  });

  group('ApiUserDirectoryRepository', () {
    late int calls;
    late ApiUserDirectoryRepository repository;

    setUp(() {
      calls = 0;
      final client = MockClient((_) async {
        calls++;
        return http.Response(
          jsonEncode([
            {
              'id': 'u1',
              'username': 'alice',
              'kind': 'Human',
              'isAdmin': false,
            },
            {
              'id': 'a1',
              'username': 'Claude research',
              'kind': 'Agent',
              'isAdmin': false,
              'handle': 'claude-research',
            },
          ]),
          200,
        );
      });
      repository = ApiUserDirectoryRepository(client, _endpoint);
    });

    test('fetches users once and shares the result', () async {
      final results = await Future.wait([
        repository.loadUsers(),
        repository.loadUsers(),
      ]);
      await repository.loadUsers();

      expect(calls, 1);
      expect(results.first.first.username, 'alice');
    });

    test("reads each user's handle, if they have one", () async {
      final users = await repository.loadUsers();

      expect(users.map((u) => u.handle), [null, 'claude-research']);
    });

    test('fetches again after invalidate', () async {
      await repository.loadUsers();
      repository.invalidate();
      await repository.loadUsers();

      expect(calls, 2);
    });

    test('reads the admin flag', () async {
      final users = await repository.loadUsers();

      expect(users.first.isAdmin, isFalse);
    });

    test('fetches again from a newly chosen server', () async {
      final hosts = <String>[];
      final client = MockClient((request) async {
        hosts.add(request.url.host);
        return http.Response(jsonEncode([]), 200);
      });
      final server = _switchableServer();
      final switching = ApiUserDirectoryRepository(client, server);

      await switching.loadUsers();
      server.use('https://second.example');
      await switching.loadUsers();

      expect(hosts, ['first.example', 'second.example']);
    });

    test('does not cache a failure', () async {
      var fail = true;
      final client = MockClient(
        (_) async =>
            fail ? http.Response('', 500) : http.Response(jsonEncode([]), 200),
      );
      final failing = ApiUserDirectoryRepository(client, _endpoint);

      await expectLater(failing.loadUsers(), throwsA(isA<ApiException>()));
      fail = false;

      expect(await failing.loadUsers(), isEmpty);
    });
  });
}
