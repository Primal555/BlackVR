#!/usr/bin/env python3
"""Batch denoise Black sound effects with FFmpeg.

The script keeps source files untouched by default and writes cleaned copies to
``black音效_denoised`` next to the original ``black音效`` directory.
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path


SUPPORTED_EXTENSIONS = {".wav", ".mp3", ".m4a", ".aac", ".ogg", ".flac"}


def project_root() -> Path:
    return Path(__file__).resolve().parents[1]


def default_input_dir() -> Path:
    return project_root() / "black音效"


def default_output_dir() -> Path:
    return project_root() / "black音效_denoised"


def require_ffmpeg(explicit_path: Path | None) -> str:
    if explicit_path is not None:
        if explicit_path.exists():
            return str(explicit_path)
        raise SystemExit(f"指定的 FFmpeg 不存在：{explicit_path}")

    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg:
        return ffmpeg

    raise SystemExit(
        "未找到 ffmpeg。请先安装 FFmpeg 并确认 ffmpeg.exe 在 PATH 中，"
        "或通过 --ffmpeg 指定 ffmpeg.exe 的完整路径。"
    )


def audio_files(input_dir: Path, recursive: bool) -> list[Path]:
    iterator = input_dir.rglob("*") if recursive else input_dir.glob("*")
    return sorted(
        path
        for path in iterator
        if path.is_file() and path.suffix.lower() in SUPPORTED_EXTENSIONS
    )


def build_filter(args: argparse.Namespace) -> str:
    filters: list[str] = []
    if args.highpass > 0:
        filters.append(f"highpass=f={args.highpass}")

    filters.append(f"afftdn=nr={args.noise_reduction}:nf={args.noise_floor}")

    if args.lowpass > 0:
        filters.append(f"lowpass=f={args.lowpass}")

    if args.normalize:
        filters.append("loudnorm=I=-16:TP=-1.5:LRA=11")

    return ",".join(filters)


def codec_args(output_path: Path, bitrate: str) -> list[str]:
    extension = output_path.suffix.lower()
    if extension in {".m4a", ".aac"}:
        return ["-c:a", "aac", "-b:a", bitrate]
    if extension == ".mp3":
        return ["-c:a", "libmp3lame", "-b:a", bitrate]
    if extension == ".ogg":
        return ["-c:a", "libvorbis", "-q:a", "5"]
    if extension == ".flac":
        return ["-c:a", "flac"]
    if extension == ".wav":
        return ["-c:a", "pcm_s16le"]
    return []


def output_path_for(source: Path, input_dir: Path, output_dir: Path) -> Path:
    relative_path = source.relative_to(input_dir)
    return output_dir / relative_path


def denoise_file(
    ffmpeg: str,
    source: Path,
    output_path: Path,
    audio_filter: str,
    args: argparse.Namespace,
) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)

    command = [
        ffmpeg,
        "-hide_banner",
        "-loglevel",
        "error",
        "-y" if args.overwrite else "-n",
        "-i",
        str(source),
        "-vn",
        "-af",
        audio_filter,
        "-map_metadata",
        "0",
        *codec_args(output_path, args.bitrate),
        str(output_path),
    ]

    if args.dry_run:
        print("DRY RUN:", " ".join(f'"{part}"' if " " in part else part for part in command))
        return

    subprocess.run(command, check=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="批量降低 black 音效目录中的背景噪声，默认不覆盖原文件。",
    )
    parser.add_argument("--input", type=Path, default=default_input_dir(), help="输入音效目录。")
    parser.add_argument("--output", type=Path, default=default_output_dir(), help="输出目录。")
    parser.add_argument("--ffmpeg", type=Path, default=None, help="ffmpeg.exe 的完整路径。")
    parser.add_argument("--recursive", action="store_true", help="递归处理子目录。")
    parser.add_argument("--overwrite", action="store_true", help="覆盖已存在的输出文件。")
    parser.add_argument("--dry-run", action="store_true", help="只打印命令，不处理文件。")
    parser.add_argument("--noise-reduction", type=float, default=18.0, help="降噪强度，单位 dB。")
    parser.add_argument("--noise-floor", type=float, default=-32.0, help="估计噪声底，单位 dB。")
    parser.add_argument("--highpass", type=float, default=60.0, help="高通滤波频率，0 表示关闭。")
    parser.add_argument("--lowpass", type=float, default=16000.0, help="低通滤波频率，0 表示关闭。")
    parser.add_argument("--normalize", action="store_true", help="额外做响度标准化。")
    parser.add_argument("--bitrate", default="192k", help="有损格式输出码率。")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    input_dir = args.input.resolve()
    output_dir = args.output.resolve()

    if not input_dir.exists() or not input_dir.is_dir():
        print(f"输入目录不存在：{input_dir}", file=sys.stderr)
        return 1

    files = audio_files(input_dir, args.recursive)
    if not files:
        print(f"没有找到可处理的音频文件：{input_dir}")
        return 0

    ffmpeg = require_ffmpeg(args.ffmpeg)
    audio_filter = build_filter(args)

    print(f"输入目录：{input_dir}")
    print(f"输出目录：{output_dir}")
    print(f"音频滤镜：{audio_filter}")
    print(f"待处理文件数：{len(files)}")

    failed: list[tuple[Path, str]] = []
    for index, source in enumerate(files, start=1):
        target = output_path_for(source, input_dir, output_dir)
        print(f"[{index}/{len(files)}] {source.name} -> {target.name}")
        try:
            denoise_file(ffmpeg, source, target, audio_filter, args)
        except subprocess.CalledProcessError as exc:
            failed.append((source, str(exc)))

    if failed:
        print("\n处理失败：", file=sys.stderr)
        for source, error in failed:
            print(f"- {source}: {error}", file=sys.stderr)
        return 1

    print("\n完成。原始文件未修改。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
