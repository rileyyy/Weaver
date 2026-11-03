import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/server_settings.dart';
import 'package:weaver/shared/data/space_lookup_cache.dart';

import '../../core/network/fakes/fixed_api_endpoint.dart';
import '../../core/network/fakes/in_memory_server_origin_store.dart';

void main() {
  late List<String> fetches;
  late Map<String, Completer<List<String>>> pending;
  late SpaceLookupCache<String> cache;

  SpaceLookupCache<String> cacheFor(endpoint) =>
      SpaceLookupCache(endpoint, (spaceId) {
        fetches.add(spaceId);
        return (pending[spaceId] = Completer<List<String>>()).future;
      });

  setUp(() {
    fetches = [];
    pending = {};
    cache = cacheFor(const FixedApiEndpoint('http://backend.test/api'));
  });

  test('fetches each Space once and shares the result', () async {
    final first = cache.load('home');
    final second = cache.load('home');
    pending['home']!.complete(['To Do']);

    expect(await first, ['To Do']);
    expect(await second, ['To Do']);
    expect(fetches, ['home']);
  });

  test('keeps each Space apart', () {
    unawaited(cache.load('home'));
    unawaited(cache.load('software'));

    expect(fetches, ['home', 'software']);
  });

  test('fetches again after invalidate', () {
    unawaited(cache.load('home'));
    cache.invalidate();
    unawaited(cache.load('home'));

    expect(fetches, ['home', 'home']);
  });

  test('does not cache a failure', () async {
    final failed = cache.load('home');
    pending['home']!.completeError(StateError('down'));
    await expectLater(failed, throwsStateError);

    unawaited(cache.load('home'));

    expect(fetches, ['home', 'home']);
  });

  test(
    'a fetch failing after invalidate leaves the newer fetch cached',
    () async {
      final stale = cache.load('home');
      final staleCompleter = pending['home']!;
      cache.invalidate();
      final fresh = cache.load('home');

      staleCompleter.completeError(StateError('down'));
      await expectLater(stale, throwsStateError);
      unawaited(cache.load('home'));

      expect(fetches, ['home', 'home']);
      pending['home']!.complete(['Done']);
      expect(await fresh, ['Done']);
    },
  );

  test('fetches again from a newly chosen server', () {
    final server = ServerSettings(
      InMemoryServerOriginStore(),
      buildTimeOrigin: 'https://first.example',
      isWeb: false,
    );
    cache = cacheFor(server);

    unawaited(cache.load('home'));
    server.use('https://second.example');
    unawaited(cache.load('home'));

    expect(fetches, ['home', 'home']);
  });
}
