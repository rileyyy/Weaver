import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/agent_admin_view_model.dart';

import 'fakes/fake_agent_admin_repository.dart';

void main() {
  late FakeAgentAdminRepository repository;
  late AgentAdminViewModel viewModel;

  setUp(() async {
    repository = FakeAgentAdminRepository([
      agentAccount('a1', name: 'Claude Code (desktop)'),
      agentAccount('a2', name: 'Nightly triage'),
    ]);
    viewModel = AgentAdminViewModel(repository);
    await viewModel.load();
  });

  tearDown(() => viewModel.dispose());

  test('loads the agents', () {
    expect(viewModel.agents.map((a) => a.name), [
      'Claude Code (desktop)',
      'Nightly triage',
    ]);
  });

  test('a failed load reports an error', () async {
    repository.error = const ApiException('down', statusCode: 503);
    final vm = AgentAdminViewModel(repository);
    addTearDown(vm.dispose);

    await vm.load();

    expect(vm.loadError, isNotNull);
  });

  test('create adds the agent in name order, trimmed', () async {
    final error = await viewModel.create(
      name: '  Codex  ',
      description: '  Runs on CI ',
    );

    expect(error, isNull);
    expect(repository.calls.single, 'create Codex Runs on CI');
    expect(viewModel.agents.map((a) => a.name), [
      'Claude Code (desktop)',
      'Codex',
      'Nightly triage',
    ]);
  });

  test('a blank description is sent as none', () async {
    await viewModel.create(name: 'Codex', description: '   ');

    expect(repository.calls.single, 'create Codex -');
  });

  test('create with a blank name asks for one without a request', () async {
    final error = await viewModel.create(name: ' ', description: '');

    expect(error, 'Enter a name.');
    expect(repository.calls, isEmpty);
  });

  test('create reports the server validation message', () async {
    repository.error = const ApiException(
      "An account named 'Codex' already exists (409).",
      statusCode: 409,
    );

    final error = await viewModel.create(name: 'Codex', description: '');

    expect(error, contains('already exists'));
  });

  test('setActive replaces the agent with the server copy', () async {
    final error = await viewModel.setActive('a2', isActive: false);

    expect(error, isNull);
    expect(viewModel.agents[1].isActive, isFalse);
  });

  test('a failed setActive falls back to a generic message', () async {
    repository.error = const ApiException('down', statusCode: 503);

    final error = await viewModel.setActive('a2', isActive: false);

    expect(error, 'Could not deactivate the agent. Try again.');
    expect(viewModel.agents[1].isActive, isTrue);
  });
}
