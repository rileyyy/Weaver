import 'dart:convert';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/server_settings.dart';
import 'package:weaver/shared/data/api_layer_repository.dart';
import 'package:weaver/shared/data/api_status_repository.dart';
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
  group('ApiLayerRepository', () {
    test('fetches layers once, with their colours', () async {
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
      final repository = ApiLayerRepository(client, _endpoint);

      await repository.loadLayers();
      final layers = await repository.loadLayers();

      expect(calls, 1);
      expect(layers.single.color, const Color(0xFF43A047));
    });

    test('fetches again after invalidate', () async {
      var calls = 0;
      final client = MockClient((_) async {
        calls++;
        return http.Response(jsonEncode([]), 200);
      });
      final repository = ApiLayerRepository(client, _endpoint);

      await repository.loadLayers();
      repository.invalidate();
      await repository.loadLayers();

      expect(calls, 2);
    });

    test('does not cache a failure', () async {
      var fail = true;
      final client = MockClient(
        (_) async =>
            fail ? http.Response('', 500) : http.Response(jsonEncode([]), 200),
      );
      final repository = ApiLayerRepository(client, _endpoint);

      await expectLater(repository.loadLayers(), throwsA(isA<ApiException>()));
      fail = false;

      expect(await repository.loadLayers(), isEmpty);
    });
  });

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
      final repository = ApiStatusRepository(client, _endpoint);

      final results = await Future.wait([
        repository.loadStatuses(),
        repository.loadStatuses(),
      ]);
      await repository.loadStatuses();

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

      await repository.loadStatuses();
      repository.invalidate();
      await repository.loadStatuses();

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

      await repository.loadStatuses();
      server.use('https://second.example');
      await repository.loadStatuses();

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
            {
              'id': 'u1',
              'username': 'alice',
              'kind': 'Human',
              'isAdmin': false,
            },
          ]),
          200,
        );
      });
      repository = ApiUserDirectoryRepository(
        client,
        _endpoint,
        now: () => now,
      );
    });

    test('reuses the list while it is fresh', () async {
      await repository.loadUsers();
      now = now.add(const Duration(minutes: 4));
      final users = await repository.loadUsers();

      expect(calls, 1);
      expect(users.single.username, 'alice');
    });

    test('fetches again after invalidate, even while fresh', () async {
      await repository.loadUsers();
      repository.invalidate();
      await repository.loadUsers();

      expect(calls, 2);
    });

    test('reads the admin flag', () async {
      final users = await repository.loadUsers();

      expect(users.single.isAdmin, isFalse);
    });

    test(
      'fetches again from a newly chosen server, even while fresh',
      () async {
        final hosts = <String>[];
        final client = MockClient((request) async {
          hosts.add(request.url.host);
          return http.Response(jsonEncode([]), 200);
        });
        final server = _switchableServer();
        final switching = ApiUserDirectoryRepository(
          client,
          server,
          now: () => now,
        );

        await switching.loadUsers();
        server.use('https://second.example');
        await switching.loadUsers();

        expect(hosts, ['first.example', 'second.example']);
      },
    );

    test('fetches again once the list is older than maxAge', () async {
      await repository.loadUsers();
      now = now.add(ApiUserDirectoryRepository.maxAge);
      await repository.loadUsers();

      expect(calls, 2);
    });
  });
}
