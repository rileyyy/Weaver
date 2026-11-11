import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/models/handle.dart';

void main() {
  group('handleProblem', () {
    for (final handle in [
      'claude-research',
      'llama3',
      'a',
      'runner-llama3-1-8b',
    ]) {
      test('accepts "$handle"', () => expect(handleProblem(handle), isNull));
    }

    for (final handle in [
      'Claude',
      'claude research',
      'claude_research',
      '@claude',
      '-claude',
      'claude-',
      'claude--research',
    ]) {
      test('rejects "$handle"', () => expect(handleProblem(handle), isNotNull));
    }

    test('rejects an overlong handle', () {
      expect(handleProblem('a' * (maxHandleLength + 1)), isNotNull);
    });
  });
}
