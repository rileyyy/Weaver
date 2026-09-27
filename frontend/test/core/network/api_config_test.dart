import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_config.dart';

void main() {
  test('web defaults to the same-origin /api path', () {
    expect(resolveApiBaseUrl(configured: '', isWeb: true), '/api');
  });

  test('appends /api to a configured origin', () {
    expect(
      resolveApiBaseUrl(configured: 'http://localhost:8080', isWeb: false),
      'http://localhost:8080/api',
    );
  });

  test('ignores trailing slashes on the configured origin', () {
    expect(
      resolveApiBaseUrl(configured: 'http://host//', isWeb: true),
      'http://host/api',
    );
  });

  test('a native build without an origin fails with a clear error', () {
    expect(
      () => resolveApiBaseUrl(configured: '', isWeb: false),
      throwsA(
        isA<StateError>().having(
          (e) => e.message,
          'message',
          contains('API_BASE_URL'),
        ),
      ),
    );
  });
}
