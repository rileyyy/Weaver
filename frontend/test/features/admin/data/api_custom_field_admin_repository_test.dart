import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/features/admin/data/api_custom_field_admin_repository.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

const _endpoint = FixedApiEndpoint('http://backend.test/api');

Map<String, dynamic> _fieldJson({String kind = 'SingleSelect'}) => {
  'id': 'field-floor',
  'typeId': 'type-room',
  'name': 'Floor',
  'kind': kind,
  'order': 0,
  'options': [
    {'id': 'option-oak', 'name': 'Oak'},
  ],
};

void main() {
  late List<http.Request> requests;

  ApiCustomFieldAdminRepository repository(Object body) {
    requests = [];
    return ApiCustomFieldAdminRepository(
      MockClient((request) async {
        requests.add(request);
        return http.Response(
          jsonEncode(body),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
      _endpoint,
    );
  }

  test("list asks for the Type's fields", () async {
    final fields = await repository([_fieldJson()]).list('type-room');

    expect(requests.single.url.path, '/api/custom-fields');
    expect(requests.single.url.queryParameters['typeId'], 'type-room');
    expect(fields.single.options.single.name, 'Oak');
  });

  test('create sends the kind by its API name and the options', () async {
    await repository(
      _fieldJson(kind: 'MultiSelect'),
    ).create('type-room', 'Wood', CustomFieldKind.multiSelect, ['Oak']);

    expect(requests.single.method, 'POST');
    expect(jsonDecode(requests.single.body), {
      'typeId': 'type-room',
      'name': 'Wood',
      'kind': 'MultiSelect',
      'options': ['Oak'],
    });
  });

  test('update sends the whole option list, new ones without an id', () async {
    await repository(_fieldJson()).update('field-floor', 'Flooring', const [
      CustomFieldOptionDraft(id: 'option-oak', name: 'Oak'),
      CustomFieldOptionDraft(name: 'Tiles'),
    ]);

    expect(requests.single.method, 'PUT');
    expect(requests.single.url.path, '/api/custom-fields/field-floor');
    expect(jsonDecode(requests.single.body), {
      'name': 'Flooring',
      'options': [
        {'id': 'option-oak', 'name': 'Oak'},
        {'id': null, 'name': 'Tiles'},
      ],
    });
  });

  test('reorder puts the order for the Type', () async {
    await repository([_fieldJson()]).reorder('type-room', ['field-floor']);

    expect(requests.single.url.path, '/api/custom-fields/order');
    expect(requests.single.url.queryParameters['typeId'], 'type-room');
    expect(jsonDecode(requests.single.body), {
      'orderedIds': ['field-floor'],
    });
  });

  test('delete removes the field', () async {
    await repository(const <String, dynamic>{}).delete('field-floor');

    expect(requests.single.method, 'DELETE');
    expect(requests.single.url.path, '/api/custom-fields/field-floor');
  });
}
