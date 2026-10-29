"""图鉴共享基础：稳定来源身份、保真画布与白名单选择集；不操作Unity或地图作者数据。"""
import hashlib
import pathlib
import re

from PIL import Image


def stable_id(app_id, container, object_id):
    """版本内容哈希从容器名剥离；独立对象身份不因显示名或像素去重而合并。"""
    logical = re.sub(r'_[0-9a-fA-F]{6,}(?=\.bundle$)', '', pathlib.Path(container).name)
    origin = '\n'.join([str(app_id), logical, str(object_id)])
    return 'kt-' + hashlib.sha256(origin.encode('utf-8')).hexdigest()[:24]


def restore_canvas(image, width, height, offset_x, offset_y):
    """PNG使用左上原点；Unity trim offset使用左下原点，恢复后仍可使用原归一化pivot。"""
    width, height, offset_x, offset_y = map(round, (width, height, offset_x, offset_y))
    if width <= 0 or height <= 0 or offset_x < 0 or offset_y < 0:
        raise ValueError('invalid canvas or trim offset')
    if image.width + offset_x > width or image.height + offset_y > height:
        raise ValueError('trimmed Sprite extends beyond original canvas; conversion needs explicit review')
    result = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    result.paste(image.convert('RGBA'), (offset_x, height - offset_y - image.height))
    return result


def selection(catalog_id, version, entries, asset_ids):
    """选择集仅传递ID和修订，拒绝未知或重复身份，不携带媒体或任意导入路径。"""
    index = {}
    for entry in entries:
        key = entry['assetId']
        if key in index:
            raise ValueError('duplicate catalog identity: ' + key)
        index[key] = entry
    chosen = []
    for key in dict.fromkeys(asset_ids):
        if key not in index:
            raise ValueError('unknown catalog identity: ' + key)
        chosen.append(dict(assetId=key, revision=index[key]['revision']))
    return dict(format='ground-workshop-selection', schemaVersion=1, catalogId=catalog_id,
                version=version, entries=chosen)


def resolve_media(root, relative):
    """媒体条目只能位于明确mediaRoot内；根本身可以由manifest相对路径重定位。"""
    root = pathlib.Path(root).resolve()
    target = (root / relative).resolve()
    if target == root or root not in target.parents:
        raise ValueError('media path escaped declared root')
    if not target.is_file():
        raise FileNotFoundError(target)
    return target


def container_rows(raw_entries, object_types):
    """源AssetBundle允许空入口；保留它的身份和原因，不因一次空指针丢掉整容器元数据。"""
    result = []
    for path, info in raw_entries:
        ptr = info['asset']
        ident, file_id = int(ptr['m_PathID']), int(ptr['m_FileID'])
        status = 'NullReference' if not ident else 'Resolved' if not file_id and ident in object_types else 'UnresolvedReference'
        result.append(dict(path=path, objectId=str(ident), fileId=file_id,
                           type=object_types.get(ident, 'Unresolved') if status=='Resolved' else 'Unresolved',
                           status=status))
    return result
