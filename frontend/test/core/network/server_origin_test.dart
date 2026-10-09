import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/server_origin.dart';

void main() {
  test('keeps a full https address', () {
    expect(
      normalizeServerOrigin('https://weaver.example.ts.net'),
      'https://weaver.example.ts.net',
    );
  });

  test('assumes https when no scheme is typed', () {
    expect(
      normalizeServerOrigin('weaver.example.ts.net'),
      'https://weaver.example.ts.net',
    );
  });

  test('keeps http and a port for local servers', () {
    expect(
      normalizeServerOrigin('http://10.0.2.2:8080'),
      'http://10.0.2.2:8080',
    );
  });

  test('trims whitespace and trailing slashes', () {
    expect(normalizeServerOrigin('  https://host//  '), 'https://host');
  });

  test('drops a pasted /api suffix', () {
    expect(normalizeServerOrigin('https://host/api/'), 'https://host');
  });

  test('keeps a sub-path the server is served under', () {
    expect(
      normalizeServerOrigin('https://host/weaver/api'),
      'https://host/weaver',
    );
  });

  for (final input in [
    '',
    '   ',
    'ftp://host',
    'https://',
    'https://host?x=1',
    'https://host#top',
    'https://user:pw@host',
  ]) {
    test('rejects "$input"', () {
      expect(normalizeServerOrigin(input), isNull);
    });
  }
}
