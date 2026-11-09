"""Export accepted sprites and mechanically cut their existing art masters."""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import shutil

from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument("--project", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
project, output = args.project, args.output
output.mkdir(parents=True, exist_ok=True)
items = project / "Source/Items"
art = project / "ArtOriginals"


def locale(language):
    with (project / f"Source/Localization/{language}.csv").open(encoding="utf-8-sig", newline="") as f:
        return {row[0]: row[1] for row in csv.reader(f) if len(row) == 2}


zh, en = locale("zh"), locale("en")
records, masters = [], []
categories = {"people": "01-人物", "seeds": "02-种子", "crops": "03-作物",
              "bottles": "04-酒瓶", "mammals": "05-牛与猪", "aquatic": "06-龙虾与海参",
              "products": "07-奶与松露", "meat": "08-肉类"}


def save_master(key, name_zh, name_en, category, source, box):
    destination = output / "02-原图独立切片" / categories[category] / f"{name_zh}__{key}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(source) as image:
        cropped = image.crop(box)
        cropped.save(destination, optimize=True)
        with Image.open(destination) as check:
            assert check.mode == cropped.mode and check.tobytes() == cropped.tobytes()
        width, height = cropped.size
    record = dict(key=key, zh=name_zh, en=name_en, category=category,
                  path=destination.relative_to(output).as_posix(), width=width, height=height,
                  source=source.relative_to(art).as_posix(), source_box=list(box),
                  sha256=hashlib.sha256(destination.read_bytes()).hexdigest())
    masters.append(record)


def bounds(source, box, threshold=128, padding=0):
    with Image.open(source) as image:
        image = image.convert("RGBA")
        subject = image.crop(box).getchannel("A").point(lambda x: 255 if x >= threshold else 0).getbbox()
    if subject is None:
        return box
    left, top, right, bottom = subject
    return (max(box[0], box[0] + left - padding), max(box[1], box[1] + top - padding),
            min(box[2], box[0] + right + padding), min(box[3], box[1] + bottom + padding))


def grid_cell(source, columns, rows, index):
    with Image.open(source) as image:
        width, height = image.size
    x, y = index % columns, index // columns
    return (round(x * width / columns), round(y * height / rows),
            round((x + 1) * width / columns), round((y + 1) * height / rows))


def runtime(key, name_zh, name_en, category, legacy=False):
    root = "03-旧版兼容单图" if legacy else "01-游戏透明单图"
    destination = output / root / categories[category] / f"{name_zh}__{key}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    source = items / (key + ".png")
    shutil.copyfile(source, destination)
    assert source.read_bytes() == destination.read_bytes()
    with Image.open(source) as image:
        width, height = image.size
        assert image.mode == "RGBA"
    records.append(dict(key=key, zh=name_zh, en=name_en, category=category, legacy=legacy,
                        path=destination.relative_to(output).as_posix(), width=width, height=height,
                        bytes=source.stat().st_size, sha256=hashlib.sha256(source.read_bytes()).hexdigest()))


crops = ["parsnip", "potato", "corn", "wheat", "hops", "strawberry", "starfruit", "ancient_fruit"]
for index, key in enumerate(crops):
    crop_name, crop_en = zh["crop_" + key], en["crop_" + key]
    fixed = key in {"parsnip", "wheat", "hops"}
    runtime(key + "_seed", zh["seed_" + key], en["seed_" + key], "seeds")
    runtime(key, crop_name + ("_正常" if not fixed else ""), crop_en + (" (Regular)" if not fixed else ""), "crops")
    if not fixed:
        runtime(key + "_small", crop_name + "_小型", crop_en + " (Small)", "crops")
        runtime(key + "_huge", crop_name + "_巨型", crop_en + " (Giant)", "crops")
        runtime(key + "_large", crop_name + "_旧版大型", crop_en + " (Legacy Large)", "crops", True)
    for sheet, category, suffix, title_zh, title_en in (
            ("crops-clean.png", "crops", "", crop_name, crop_en),
            ("seeds-clean.png", "seeds", "_seed", zh["seed_" + key], en["seed_" + key])):
        source = art / sheet
        box = bounds(source, grid_cell(source, 4, 2, index))
        save_master(key + suffix, title_zh, title_en, category, source, box)
runtime("parsnip_small", "防风草_旧版小型", "Parsnip (Legacy Small)", "crops", True)
runtime("parsnip_large", "防风草_旧版大型", "Parsnip (Legacy Large)", "crops", True)

for key, name_zh, name_en, sheet in (
        ("farmer", "老乡_种植装", "Fellow Townsman (Farming)", "farmer-front-012.png"),
        ("farmer_ranch", "老乡_牧场装", "Fellow Townsman (Ranching)", "Husbandry/farmer-ranch.png")):
    runtime(key, name_zh, name_en, "people")
    if key == "farmer":
        source = art / sheet
        with Image.open(source) as image:
            box = (0, 0, *image.size)
        save_master(key, name_zh, name_en, "people", source, bounds(source, box))

for sheet, columns, keys in (
        ("beer-bottles-012.png", 2, ["beer", "pale_ale"]),
        ("fruit-bottles-012.png", 3, ["strawberry_wine", "starfruit_wine"]),
        ("ancient-wine-light-blue-100.png", 1, ["ancient_wine"])):
    source = art / sheet
    for index, key in enumerate(keys):
        runtime("bottle_" + key, zh["wine_" + key], en["wine_" + key], "bottles")
        save_master("bottle_" + key, zh["wine_" + key], en["wine_" + key], "bottles", source,
                    bounds(source, grid_cell(source, columns, 1, index)))

stages_zh = {"baby": "初生", "young": "幼年", "female": "成年母", "male": "成年公",
             "roe": "卵", "fry": "幼体", "small": "小型", "medium": "中型", "large": "大型"}
for entry in json.loads((project / "Design/farm-art-exports.json").read_text("utf-8-sig")):
    key = entry["key"]
    if key == "farmer_ranch":
        category, title_zh, title_en = "people", "老乡_牧场装", "Fellow Townsman (Ranching)"
    elif key.startswith(("cow_", "pig_")):
        species, stage = key.split("_")
        species_zh = "牛" if species == "cow" else "猪"
        title_zh = stages_zh[stage] + species_zh
        title_en = ("Cattle" if species == "cow" else "Pig") + " (" + stage.capitalize() + ")"
        category = "mammals"
    elif key.startswith("lobster_meat_") or key.startswith(("beef_", "pork_")):
        title_zh, title_en, category = zh["farm_" + key], en["farm_" + key], "meat"
    elif key.startswith(("lobster_", "sea_cucumber_")):
        species, stage = key.rsplit("_", 1)
        species_zh = "龙虾" if species == "lobster" else "海参"
        title_zh = species_zh + "_" + stages_zh[stage]
        title_en = ("Lobster" if species == "lobster" else "Sea Cucumber") + " (" + stage.capitalize() + ")"
        category = "aquatic"
    else:
        title_zh, title_en, category = zh["farm_" + key], en["farm_" + key], "products"
    if key != "farmer_ranch":
        runtime(key, title_zh, title_en, category)
    x, y, width, height = map(int, entry["source_clip"].split(","))
    save_master(key, title_zh, title_en, category, art / "Husbandry" / entry["source"],
                (x, y, x + width, y + height))

assert len(records) == 71 and len({r["key"] for r in records}) == 71
assert len(masters) == 54 and len({r["key"] for r in masters}) == 54
assert {r["key"] for r in records} == {p.stem for p in items.glob("*.png")}
assert sum(not r["legacy"] for r in records) == 64

font_path = "C:/Windows/Fonts/msyh.ttc"
title_font = ImageFont.truetype(font_path, 46)
subtitle_font = ImageFont.truetype(font_path, 23)
name_font = ImageFont.truetype(font_path, 25)
key_font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
preview = output / "04-预览总览"
preview.mkdir(exist_ok=True)


def contact_sheet(name, title, subtitle, rows, columns=4):
    width, padding, gap, tile_height = 1680, 48, 18, 310
    tile_width = (width - 2 * padding - (columns - 1) * gap) // columns
    count_rows = math.ceil(len(rows) / columns)
    height = 168 + count_rows * (tile_height + gap) + 66
    canvas = Image.new("RGB", (width, height), "#f2ece1")
    draw = ImageDraw.Draw(canvas)
    draw.text((padding, 30), title, font=title_font, fill="#302e28")
    draw.text((padding, 98), subtitle, font=subtitle_font, fill="#686255")
    for i, row in enumerate(rows):
        x = padding + (i % columns) * (tile_width + gap)
        y = 158 + (i // columns) * (tile_height + gap)
        draw.rounded_rectangle((x, y, x + tile_width, y + tile_height), radius=12, fill="#fffdf8", outline="#d8d0c2", width=2)
        box = (x + 12, y + 12, x + tile_width - 12, y + 238)
        with Image.open(output / row["path"]) as image:
            image = image.convert("RGBA")
            image.thumbnail((box[2] - box[0], box[3] - box[1]), Image.Resampling.NEAREST)
            canvas.paste(image, (box[0] + (box[2] - box[0] - image.width) // 2,
                                 box[1] + (box[3] - box[1] - image.height) // 2), image)
        label = row["zh"].replace("_", " · ").replace("（标准）", "·标准").replace("（小）", "·小").replace("（大）", "·大")
        draw.text((x + 14, y + 248), label, font=name_font, fill="#302e28")
        draw.text((x + 14, y + 283), row["key"], font=key_font, fill="#777066")
    draw.text((padding, height - 49), "老乡来了 v1.1.7  |  美术目录预览 / Art index  |  单张透明 PNG 见素材包", font=subtitle_font, fill="#686255")
    destination = preview / name
    canvas.save(destination, optimize=True)


contact_sheet("01-八种作物与种子.png", "八种作物与种子", "原图独立切片 · 8 种作物 + 8 种种子", [r for r in masters if r["category"] in {"crops", "seeds"}])
contact_sheet("02-老乡与五种酒.png", "老乡与五种酒", "两套服装 · 小麦啤酒 / 淡啤酒 / 草莓果酒 / 杨桃酒 / 上古果酒", [r for r in masters if r["category"] in {"people", "bottles"}])
contact_sheet("03-牛猪与养殖产物.png", "牛、猪与养殖产物", "成长与公母形态 · 牛奶 · 两档黑松露 · 牛肉与猪肉", [r for r in masters if r["category"] in {"mammals", "products"} or r["key"].startswith(("beef_", "pork_"))])
contact_sheet("04-龙虾与海参.png", "龙虾与海参", "卵、幼体与成长形态 · 四档龙虾肉", [r for r in masters if r["category"] == "aquatic" or r["key"].startswith("lobster_meat_")])
contact_sheet("05-作物尺寸变化.png", "作物尺寸变化", "游戏实际贴图 · 五种可变尺寸作物与三种固定尺寸作物", [r for r in records if r["category"] == "crops" and not r["legacy"]])

catalog = dict(version="1.1.7", runtime_sprites=71, current_sprites=64, legacy_sprites=7,
               unique_master_cuts=54, runtime=records, masters=masters)
(output / "素材目录.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", "utf-8")
with (output / "素材目录.csv").open("w", encoding="utf-8-sig", newline="") as f:
    fields = ["kind", "key", "zh", "en", "path", "width", "height", "sha256"]
    writer = csv.DictWriter(f, fields, extrasaction="ignore")
    writer.writeheader()
    writer.writerows(dict(r, kind="legacy" if r["legacy"] else "current") for r in records)
    writer.writerows(dict(r, kind="master-cut") for r in masters)
(output / "00-素材说明.zh-en.txt").write_text("""老乡来了 v1.1.7 — 独立美术素材 / Art assets

01-游戏透明单图：64张当前使用贴图，逐文件等同于发行版，按人物、种子、作物、酒瓶、牛猪、水产、产物、肉类归类。中文名后保留原素材ID。
02-原图独立切片：54张不同画面，从已有定稿原画按既有导出边界机械切分，保持原始像素，不放大、不重绘。适合选图、排版和宣传。
03-旧版兼容单图：7张遗留作物尺寸图，只供旧存档兼容；不计入新增玩法或当前宣传数量。
04-预览总览：5张索引预览。它们是素材目录展示，不是游戏截图。
素材目录.csv / .json：中英文名称、素材ID、尺寸、路径、SHA256及原图裁剪坐标。

71张运行贴图不等于71种新物品：包含同一作物的尺寸变体、动物成长与公母形态、角色服装及7张兼容图。54张原图切片按不同画面去重，属于同一套已有定稿美术；本次未重新生成美术。
透明单图保留原透明背景；预览页使用浅色底方便阅读。原画来源与生成记录仍保存在完整源码美术包的 ArtOriginals 内。

01: 64 current transparent game sprites, byte-identical to the release, grouped by subject. Filenames retain the original asset keys.
02: 54 distinct master-art cuts, mechanically cropped from existing final masters using the established export bounds. No upscaling or repainting.
03: 7 legacy crop sprites for saved-item compatibility; excluded from current gameplay and promotional counts.
04: 5 index previews, composed from the assets. These are art contact sheets, not gameplay screenshots.
The CSV/JSON catalog records bilingual names, keys, dimensions, paths, SHA256 hashes and master crop coordinates.
71 runtime sprites include size, stage, sex, outfit and legacy variants; they are not 71 distinct added items. Original masters and provenance records remain in the full source/art archive.
""", "utf-8")
print(json.dumps({"runtime": len(records), "current": 64, "legacy": 7, "masters": len(masters), "previews": 5}))
