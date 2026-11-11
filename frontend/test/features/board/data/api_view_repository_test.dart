import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/api_view_repository.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/shared/models/tag_match.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

Map<String, dynamic> _viewJson({
  String id = 'view-1',
  String name = 'Outside',
  bool builtIn = false,
  String layout = 'Roadmap',
}) => {
  'id': id,
  'name': name,
  'builtIn': builtIn,
  'spaceIds': ['space-home'],
  'text': 'fence',
  'tags': ['outside'],
  'tagMatch': 'All',
  'applyDoneHiding': false,
  'scheduledOrInProgressOnly': true,
  'layout': layout,
};

http.Response _json(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

void main() {
  const endpoint = FixedApiEndpoint('http://host/api');

  test('list reads every input of each View', () async {
    final repository = ApiViewRepository(
      MockClient((request) async {
        expect(request.url.path, '/api/views');
        return _json([
          _viewJson(id: 'overview', name: 'Overview', builtIn: true),
          _viewJson(),
        ]);
      }),
      endpoint,
    );

    final views = await repository.list();

    expect(views.map((v) => v.name), ['Overview', 'Outside']);
    expect(views.first.builtIn, isTrue);
    final definition = views.last.definition;
    expect(definition.spaceIds, {'space-home'});
    expect(definition.text, 'fence');
    expect(definition.tags, {'outside'});
    expect(definition.tagMatch, TagMatch.all);
    expect(definition.applyDoneHiding, isFalse);
    expect(definition.scheduledOrInProgressOnly, isTrue);
    expect(definition.layout, ViewLayout.roadmap);
  });

  test('a layout this client does not know opens as the Board', () async {
    final repository = ApiViewRepository(
      MockClient((_) async => _json([_viewJson(layout: 'Agenda')])),
      endpoint,
    );

    final view = (await repository.list()).single;

    expect(view.definition.layout, ViewLayout.board);
  });

  test('create posts the whole definition', () async {
    late Map<String, dynamic> sent;
    final repository = ApiViewRepository(
      MockClient((request) async {
        expect(request.method, 'POST');
        expect(request.url.path, '/api/views');
        sent = jsonDecode(request.body) as Map<String, dynamic>;
        return _json(_viewJson(), 201);
      }),
      endpoint,
    );

    await repository.create(
      const ViewDefinition(
        name: 'Outside',
        spaceIds: {'space-home'},
        tags: {'outside'},
        tagMatch: TagMatch.all,
        applyDoneHiding: false,
        layout: ViewLayout.hierarchy,
      ),
    );

    expect(sent, {
      'name': 'Outside',
      'spaceIds': ['space-home'],
      'text': null,
      'tags': ['outside'],
      'tagMatch': 'All',
      'applyDoneHiding': false,
      'scheduledOrInProgressOnly': false,
      'layout': 'Hierarchy',
    });
  });

  test('update puts to the View, and delete removes it', () async {
    final calls = <String>[];
    final repository = ApiViewRepository(
      MockClient((request) async {
        calls.add('${request.method} ${request.url.path}');
        return request.method == 'DELETE'
            ? http.Response('', 204)
            : _json(_viewJson());
      }),
      endpoint,
    );

    await repository.update('view-1', const ViewDefinition(name: 'Renamed'));
    await repository.delete('view-1');

    expect(calls, ['PUT /api/views/view-1', 'DELETE /api/views/view-1']);
  });

  test('the default is read and set on its own resource', () async {
    final calls = <String>[];
    final repository = ApiViewRepository(
      MockClient((request) async {
        calls.add('${request.method} ${request.url.path} ${request.body}');
        return _json(_viewJson());
      }),
      endpoint,
    );

    await repository.loadDefault();
    await repository.setDefault('view-1');

    expect(calls, [
      'GET /api/views/default ',
      'PUT /api/views/default {"viewId":"view-1"}',
    ]);
  });

  test('a name already taken fails with the server status', () async {
    final repository = ApiViewRepository(
      MockClient((_) async => _json({'detail': 'taken'}, 409)),
      endpoint,
    );

    await expectLater(
      repository.create(const ViewDefinition(name: 'Outside')),
      throwsA(isA<ApiException>().having((e) => e.statusCode, 'status', 409)),
    );
  });
}
