# unity_sponsorpatch

Unity 2022.3.22f1용 스폰서 텍스처 병합 에디터 툴입니다.

야구복처럼 UV 언랩이 끝난 원본 텍스처 위에, 같은 위치 기준으로 잘라 둔 투명 배경 스폰서 키트 텍스처를 포토샵 레이어처럼 합성합니다.

원본 PNG 파일을 덮어쓰지 않는 비파괴 방식입니다. Unity가 Play Mode에 들어가면 임시 병합 텍스처와 임시 머티리얼을 만들고, Play Mode가 끝나면 원래 머티리얼 상태로 되돌립니다.

## 설치

### VCC로 설치

[Add to VCC](vcc://vpm/addRepo?url=https%3A%2F%2Fraw.githubusercontent.com%2Fsinonmiyaki%2Funity_sponsorpatch%2Fmain%2Findex.json)

위 링크가 열리지 않으면 VCC의 `Settings > Packages > Add Repository`에 아래 URL을 넣으면 됩니다.

```text
https://raw.githubusercontent.com/sinonmiyaki/unity_sponsorpatch/main/index.json
```

VCC에 repository를 추가한 뒤 프로젝트의 `Manage Project`에서 `YUMESORA Merge Sponsor`를 추가합니다.

### unitypackage로 설치

GitHub Releases에서 `YUMESORA_MergeSponsor.unitypackage`를 내려받아 Unity 프로젝트로 드래그해서 임포트해도 됩니다.

직접 폴더로 넣고 싶다면 이 저장소의 `Assets/YUMESORA/MergeSponsor` 폴더를 Unity 프로젝트의 `Assets` 폴더 아래에 넣어도 됩니다.

Unity 상단 메뉴에 `YUMESORA > Create Sponsor Tool`이 생깁니다.
Hierarchy 우클릭 메뉴에는 `YUMESORA > Create Sponsor Tool`이 생깁니다.

## 사용법

1. Hierarchy에서 아바타 루트를 우클릭합니다.
2. `YUMESORA > Create Sponsor Tool`을 누릅니다.
3. 생성된 `YUMESORA Sponsor Tool` 오브젝트의 Inspector를 엽니다.
4. `Original Texture`에 유니폼 원본 텍스처를 넣습니다.
5. `Sponsor Kits`에 스폰서 키트를 필요한 만큼 추가합니다.
6. 필요하면 각 스폰서 키트의 이름, 활성 여부, opacity를 조정합니다.
7. VRChat 업로드를 진행하거나 Unity Play Mode에 들어가면 임시 병합 텍스처가 적용됩니다.
8. Play Mode가 끝나면 원래 머티리얼 텍스처로 복원됩니다.

머티리얼에서 원본 텍스처를 사용하는 슬롯만 교체합니다. 기본 텍스처 프로퍼티는 `_MainTex`이며, 필요하면 Inspector의 `Texture Property`에서 바꿀 수 있습니다.

생성된 Sponsor Tool 오브젝트는 `EditorOnly` 태그를 사용합니다. 이 오브젝트는 업로드 결과물에 포함되지 않고, Play/VRC 업로드 준비 중에만 임시 병합을 수행하는 도우미 역할을 합니다.

## 추가 기능

- 비파괴 적용: 원본 PNG와 스폰서 키트 PNG 파일은 수정하지 않습니다.
- Play/VRC 업로드 시 임시 적용: Play Mode 진입 시 임시 텍스처와 임시 머티리얼을 만들고, 종료 시 정리합니다.
- 여러 스폰서 키트: 스폰서 키트를 여러 장 등록하고 순서대로 합성할 수 있습니다.
- 자동 리사이즈: 스폰서 키트 해상도가 원본과 다르면 원본 크기에 맞춰 리사이즈해서 합성할 수 있습니다.
- Hierarchy 우클릭 생성: 아바타를 우클릭해서 대기 상태의 Sponsor Tool 오브젝트를 바로 만들 수 있습니다.

## 권장 텍스처 준비

- 원본과 스폰서 키트는 가능하면 같은 픽셀 크기로 준비하는 것이 가장 정확합니다.
- 4096 x 4096 원본이면 스폰서 키트도 4096 x 4096 투명 PNG로 준비하는 것을 권장합니다.
- 2048 x 2048처럼 크기만 다르고 비율이 같은 스폰서 키트는 자동 리사이즈로 원본 크기에 맞춰 적용됩니다.
- 원본과 스폰서 키트의 가로세로 비율이 다르면 왜곡을 막기 위해 적용되지 않습니다.
- 스폰서가 없는 영역은 완전 투명 alpha 0으로 둡니다.
- 원본 텍스처가 머티리얼의 `_MainTex`가 아닌 다른 프로퍼티에 들어가 있다면 `Texture Property`를 맞춰야 합니다.
- Unity import max size가 2048로 제한된 텍스처는 Play 중에도 2048 기준으로 읽힐 수 있습니다. 4096 결과가 필요하면 텍스처 import max size도 4096 이상으로 맞추는 것을 권장합니다.

## 릴리즈 만들기

로컬에서 패키지를 다시 만들려면 다음 명령을 실행합니다.

```bash
bash scripts/build-unitypackage.sh
bash scripts/build-vpm-package.sh
```

GitHub Release는 태그를 푸시하면 자동으로 만들어집니다.

```bash
git add .
git commit -m "Release YUMESORA Merge Sponsor"
git push origin main
git tag v1.1.0
git push origin v1.1.0
```

태그가 푸시되면 GitHub Actions가 `dist/YUMESORA_MergeSponsor.unitypackage`와 `dist/com.yumesora.merge-sponsor-1.1.0.zip`을 빌드하고, 둘 다 Release asset으로 업로드합니다.

VCC용 repository listing은 `index.json`입니다. 공개 링크는 다음 주소입니다.

```text
https://raw.githubusercontent.com/sinonmiyaki/unity_sponsorpatch/main/index.json
```

Add to VCC 링크는 다음 주소입니다.

```text
vcc://vpm/addRepo?url=https%3A%2F%2Fraw.githubusercontent.com%2Fsinonmiyaki%2Funity_sponsorpatch%2Fmain%2Findex.json
```
