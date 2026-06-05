# unity_sponsorpatch

Unity 2022.3.22f1용 스폰서 텍스처 병합 에디터 툴입니다.

야구복처럼 UV 언랩이 끝난 원본 PNG 위에, 같은 위치 기준으로 잘라 둔 투명 배경 스폰서 키트 PNG를 포토샵 레이어처럼 합성합니다.

`Apply`를 누르면 원본 PNG 파일 자체가 덮어써지고, 적용 전 파일은 같은 폴더에 `원본파일명_backup.png`로 백업됩니다.

## 설치

GitHub Releases에서 `YUMESORA_MergeSponsor.unitypackage`를 내려받아 Unity 프로젝트로 드래그해서 임포트하면 됩니다.

직접 폴더로 넣고 싶다면 이 저장소의 `Assets/YUMESORA/MergeSponsor` 폴더를 Unity 프로젝트의 `Assets` 폴더 아래에 넣어도 됩니다.

Unity 상단 메뉴에 `YUMESORA > Merge Sponsor`가 생깁니다.

## 사용법

1. `YUMESORA > Merge Sponsor`를 엽니다.
2. `Original PNG`에 원본 유니폼 PNG를 넣습니다.
3. `Sponsor Kit PNG`에 투명 배경 스폰서 키트 PNG를 넣습니다.
4. 필요하면 `Sponsor Opacity`, 자동 리사이즈 옵션을 조정합니다.
5. `Apply To Original`을 누르면 원본 PNG가 병합 결과로 덮어써집니다.

처음 적용할 때 `uniform.png`가 선택되어 있었다면 같은 폴더에 `uniform_backup.png`가 만들어집니다.

적용을 잘못했다면 같은 원본 PNG를 선택한 상태에서 `Revert From Backup`을 누르면 됩니다. 백업 PNG가 원본에 다시 복사되고, 백업 파일은 삭제됩니다.

## 추가 기능

- 미리보기: 합성 결과를 툴 안에서 확인할 수 있습니다.
- 자동 리사이즈: 스폰서 키트 해상도가 원본과 다르면 원본 크기에 맞춰 리사이즈해서 합성할 수 있습니다.
- 되돌리기: `원본파일명_backup.png`를 원본에 복원하고 백업 파일을 삭제합니다.
- import size 유지: 4096 같은 원본 해상도가 Unity max size 때문에 줄어들지 않도록 원본 import size를 맞춥니다.

## 권장 텍스처 준비

- 원본과 스폰서 키트는 가능하면 같은 픽셀 크기로 준비하는 것이 가장 정확합니다.
- 4096 x 4096 원본이면 스폰서 키트도 4096 x 4096 투명 PNG로 준비하는 것을 권장합니다.
- 2048 x 2048처럼 크기만 다르고 비율이 같은 스폰서 키트는 자동 리사이즈로 원본 크기에 맞춰 적용됩니다.
- 원본과 스폰서 키트의 가로세로 비율이 다르면 왜곡을 막기 위해 적용되지 않습니다.
- 스폰서가 없는 영역은 완전 투명 alpha 0으로 둡니다.
- 백업 파일은 Revert 전까지 삭제하지 않는 것이 좋습니다.

## 릴리즈 만들기

로컬에서 패키지를 다시 만들려면 다음 명령을 실행합니다.

```bash
bash scripts/build-unitypackage.sh
```

GitHub Release는 태그를 푸시하면 자동으로 만들어집니다.

```bash
git add .
git commit -m "Release YUMESORA Merge Sponsor"
git push origin main
git tag v1.0.0
git push origin v1.0.0
```

태그가 푸시되면 GitHub Actions가 `dist/YUMESORA_MergeSponsor.unitypackage`를 빌드하고, 같은 이름의 파일을 Release asset으로 업로드합니다.
