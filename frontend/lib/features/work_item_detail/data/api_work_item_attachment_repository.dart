import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/work_item_detail/data/work_item_attachment_repository.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';

@LazySingleton(as: WorkItemAttachmentRepository)
class ApiWorkItemAttachmentRepository implements WorkItemAttachmentRepository {
  ApiWorkItemAttachmentRepository(http.Client client, ApiEndpoint endpoint)
    : _api = JsonApiClient(client, endpoint);

  final JsonApiClient _api;

  @override
  Future<List<WorkItemAttachment>> loadAttachments(String workItemId) =>
      _api.get(
        '/work-items/$workItemId/attachments',
        (json) => JsonApiClient.listOf(json, WorkItemAttachment.fromJson),
        failureMessage: 'Failed to load attachments',
      );

  @override
  Future<WorkItemAttachment> upload(String workItemId, AttachmentUpload file) =>
      _api.postMultipart(
        '/work-items/$workItemId/attachments',
        [
          http.MultipartFile.fromBytes(
            'file',
            file.bytes,
            filename: file.fileName,
            contentType: MediaType.parse(file.contentType),
          ),
        ],
        _attachment,
        failureMessage: 'Failed to upload the attachment',
      );

  @override
  Future<Uint8List> downloadContent(String attachmentId) => _api.getBytes(
    '/work-item-attachments/$attachmentId/content',
    failureMessage: 'Failed to download the attachment',
  );

  @override
  Future<Uint8List> downloadThumbnail(String attachmentId) => _api.getBytes(
    '/work-item-attachments/$attachmentId/thumbnail',
    failureMessage: 'Failed to load the thumbnail',
  );

  @override
  Future<void> delete(String attachmentId) => _api.delete(
    '/work-item-attachments/$attachmentId',
    failureMessage: 'Failed to delete the attachment',
  );

  static WorkItemAttachment _attachment(Object? json) =>
      WorkItemAttachment.fromJson(json as Map<String, dynamic>);
}
