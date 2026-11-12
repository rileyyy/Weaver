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

  group('partialHandlePattern', () {
    final partial = RegExp('^$partialHandlePattern\$');

    for (final typed in [
      '',
      'c',
      'clau',
      'claude-',
      'claude-r',
      'claude-research',
      'llama3-',
    ]) {
      test('accepts "$typed"', () => expect(partial.hasMatch(typed), isTrue));
    }

    for (final typed in [
      '-',
      '--',
      '-x',
      'claude--',
      'claude--r',
      'Claude',
      'claude_r',
      'claude r',
    ]) {
      test('rejects "$typed"', () => expect(partial.hasMatch(typed), isFalse));
    }

    test('every whole handle is also a partial one', () {
      for (final handle in ['claude-research', 'a', 'runner-llama3-1-8b']) {
        expect(partial.hasMatch(handle), isTrue, reason: handle);
      }
    });
  });
}
