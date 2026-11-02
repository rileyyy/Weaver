import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/agent_keys_view_model.dart';
import 'package:weaver/features/admin/models/api_key_scope.dart';

import 'fakes/fake_agent_admin_repository.dart';

void main() {
  late FakeAgentAdminRepository repository;
  late AgentKeysViewModel viewModel;

  setUp(() async {
    repository = FakeAgentAdminRepository(
      [agentAccount('a1')],
      keys: {
        'a1': [apiKey('k1', label: 'laptop')],
      },
    );
    viewModel = AgentKeysViewModel(repository, 'a1');
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test("loads the agent's keys", () {
    expect(viewModel.keys.map((k) => k.label), ['laptop']);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);
    final vm = AgentKeysViewModel(repository, 'a1');
    addTearDown(vm.dispose);

    await vm.load();

    expect(vm.loadError, isNotNull);
  });

  test('issuing returns the plaintext key once and lists the key', () async {
    final result = await viewModel.issueKey(
      label: ' desktop ',
      scope: ApiKeyScope.readWrite,
    );

    expect(result.error, isNull);
    expect(result.key, 'wvr_secret-desktop');
    expect(repository.calls.single, 'issueKey a1 desktop ReadWrite');
    expect(viewModel.keys.map((k) => k.label), ['laptop', 'desktop']);
  });

  test('issuing with a blank label asks for one without a request', () async {
    final result = await viewModel.issueKey(
      label: '  ',
      scope: ApiKeyScope.readOnly,
    );

    expect(result.key, isNull);
    expect(result.error, 'Enter a label.');
    expect(repository.calls, isEmpty);
  });

  test('a refused issue reports why and returns no key', () async {
    repository.error = const ApiException(
      'Label must be at most 100 characters (400).',
      statusCode: 400,
    );

    final result = await viewModel.issueKey(
      label: 'x',
      scope: ApiKeyScope.readOnly,
    );

    expect(result.key, isNull);
    expect(result.error, contains('at most 100'));
  });

  test('a revoked key stays listed, marked revoked', () async {
    final error = await viewModel.revokeKey('k1');

    expect(error, isNull);
    expect(repository.calls.single, 'revokeKey a1 k1');
    expect(viewModel.keys.single.isRevoked, isTrue);
  });

  test('a failed revoke falls back to a generic message', () async {
    repository.error = const ApiException('down', statusCode: 503);

    final error = await viewModel.revokeKey('k1');

    expect(error, 'Could not revoke the key. Try again.');
  });
}
