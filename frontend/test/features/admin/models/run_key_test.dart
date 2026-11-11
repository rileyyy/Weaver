import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/run_key.dart';

import '../fakes/fake_run_report_repository.dart';

void main() {
  test('two keys with the same account, key and run id are equal', () {
    const a = RunKey(accountId: 'a1', apiKeyId: 'k1', runId: 'run 1');
    const b = RunKey(accountId: 'a1', apiKeyId: 'k1', runId: 'run 1');

    expect(a, b);
    expect(a.hashCode, b.hashCode);
  });

  test('keys differing in any part are not equal', () {
    const base = RunKey(accountId: 'a1', apiKeyId: 'k1', runId: 'run 1');

    expect(
      base,
      isNot(const RunKey(accountId: 'a2', apiKeyId: 'k1', runId: 'run 1')),
    );
    expect(
      base,
      isNot(const RunKey(accountId: 'a1', apiKeyId: 'k2', runId: 'run 1')),
    );
    expect(
      base,
      isNot(const RunKey(accountId: 'a1', apiKeyId: 'k1', runId: 'run 2')),
    );
  });

  test('a separator in a free-text part cannot make two runs collide', () {
    const a = RunKey(accountId: 'a', apiKeyId: 'k', runId: 'x|y');
    const b = RunKey(accountId: 'a', apiKeyId: 'k|x', runId: 'y');

    expect(a, isNot(b));
  });

  test("a run report's key is its account, key and run id", () {
    final run = runReport('run-1');

    expect(
      run.key,
      const RunKey(accountId: 'agent-1', apiKeyId: 'key-1', runId: 'run-1'),
    );
  });
}
