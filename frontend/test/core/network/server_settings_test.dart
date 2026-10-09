import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/server_origin_store.dart';
import 'package:weaver/core/network/server_settings.dart';

import 'fakes/in_memory_server_origin_store.dart';

class _FailingStore implements ServerOriginStore {
  @override
  Future<String?> readOrigin() async => null;

  @override
  Future<void> writeOrigin(String origin) async =>
      throw Exception('storage unavailable');
}

void main() {
  ServerSettings native({
    ServerOriginStore? store,
    String buildTimeOrigin = '',
  }) => ServerSettings(
    store ?? InMemoryServerOriginStore(),
    buildTimeOrigin: buildTimeOrigin,
    isWeb: false,
  );

  group('on web', () {
    test('uses the same-origin /api path and cannot be changed', () {
      final settings = webServerSettings();

      expect(settings.baseUrl, '/api');
      expect(settings.canChangeServer, isFalse);
      expect(settings.isConfigured, isTrue);
    });

    test('uses a build-time origin when one is given', () {
      final settings = ServerSettings(
        InMemoryServerOriginStore(),
        buildTimeOrigin: 'http://localhost:8080/',
        isWeb: true,
      );

      expect(settings.baseUrl, 'http://localhost:8080/api');
    });

    test('ignores a remembered server', () async {
      final settings = ServerSettings(
        InMemoryServerOriginStore('https://elsewhere'),
        buildTimeOrigin: '',
        isWeb: true,
      );

      await settings.load();

      expect(settings.baseUrl, '/api');
    });
  });

  group('on native', () {
    test('has no server until one is chosen', () {
      final settings = native();

      expect(settings.canChangeServer, isTrue);
      expect(settings.isConfigured, isFalse);
      expect(settings.origin, isNull);
      expect(() => settings.baseUrl, throwsStateError);
    });

    test('starts from the build-time origin', () {
      final settings = native(buildTimeOrigin: 'http://10.0.2.2:8080');

      expect(settings.baseUrl, 'http://10.0.2.2:8080/api');
    });

    test(
      'load restores the remembered server over the build-time one',
      () async {
        final settings = native(
          store: InMemoryServerOriginStore('https://weaver.example.ts.net'),
          buildTimeOrigin: 'http://10.0.2.2:8080',
        );

        await settings.load();

        expect(settings.baseUrl, 'https://weaver.example.ts.net/api');
      },
    );

    test('use switches servers without remembering', () async {
      final store = InMemoryServerOriginStore();
      final settings = native(store: store)..use('https://a.example');

      expect(settings.baseUrl, 'https://a.example/api');
      expect(store.origin, isNull);

      await settings.remember();

      expect(store.origin, 'https://a.example');
    });

    test('remember survives storage failures', () async {
      final settings = native(store: _FailingStore())..use('https://a.example');

      await expectLater(settings.remember(), completes);
    });
  });
}
