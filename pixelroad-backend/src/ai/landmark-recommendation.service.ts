// 랜드마크 추천
import { Injectable } from "@nestjs/common";
import { VisitedLandmarkDto } from "./dto/aiReport.dto";
import { Landmark } from "../landmark/landmark.interface";

@Injectable()
export class LandmarkRecommendationService {
    /**
     * 방문 기록을 기반으로 추천 랜드마크를 선택한다.
     *
     * 추천 기준
     * 1. 카테고리별 총 방문 횟수를 계산한다.
     * 2. 방문 횟수가 높은 카테고리부터 확인한다.
     * 3. 해당 카테고리에 미방문 랜드마크가 있으면 그중 1개를 추천한다.
     * 4. 없으면 다음 순위 카테고리를 확인한다.
     * 5. 모든 선호 카테고리에 미방문 랜드마크가 없다면
     *    다른 미방문 랜드마크 중 1개를 추천한다.
     */

    // 추천 랜드마크 선택
    selectRecommendation(visitedLandmarks: VisitedLandmarkDto[],
        allLandmarks: Landmark[]): Landmark | undefined {
        // 방문한 랜드마크 ID
        const visitedIds = new Set(
            visitedLandmarks.map((landmark) => landmark.landmarkId),
        );

        // // 방문한 랜드마크아이디
        // const visited = allLandmarks.filter(
        //     (landmark) => visitedIds.has(landmark.id),
        // );

        // 3 미방문 랜드마크만 필터링
        const unvisited = allLandmarks.filter(
            (landmark) => !visitedIds.has(landmark.id),
        );

        // 미방문 랜드마크가 없으면 추천하지 않음
        if (unvisited.length === 0) {
            return undefined;
            console.log("추천할 랜드마크가 없습니다");
        }


        // 카테고리별 총 방문 횟수
        const categoryVisitCounts = new Map<string, number>();

        for (const visited of visitedLandmarks) {
            const landmark = allLandmarks.find(
                (landmark) => landmark.id === visited.landmarkId
            );
            if (!landmark) continue;

            const currentCount = categoryVisitCounts.get(
                landmark.category) ?? 0;

            categoryVisitCounts.set(landmark.category,
                currentCount + visited.visitCount);
        }

        // 방문 횟수가 높은 카테고리 순으로 정렬
        const categoryOrder = [...categoryVisitCounts.entries()]
            .sort((a, b) => b[1] - a[1]);

        // 1순위 -> 2순위 -> 3순위 ...
        for (const [category] of categoryOrder) {
            // 해당 카테고리의 미방문 랜드마크
            const candidates = unvisited.filter(
                (landmark) => landmark.category === category
            );

            // 미방문 랜드마크가 있으면 랜덤 1개 추천
            if (candidates.length > 0) {
                const randomIndex = Math.floor(
                    Math.random() * candidates.length);

                return candidates[randomIndex];
            }
        }

        // 선호 카테고리에 미방문 랜드마크가 하나도 없는 경우
        // 추천하지않음
        return undefined;
        // 전체 미방문 랜드마크 중 하나 추천
        // const randomIndex =  Math.floor(
        //     Math.random() * unvisited.length);

        // return unvisited[randomIndex];


        // // 가장 많이 방문한 카테고리
        // let preferredCategory: string | undefined;
        // let maxVisitCount = 0;

        // for (const [category, count] of categoryVisitCounts) {
        //     if (count > maxVisitCount) {
        //         maxVisitCount = count;
        //         preferredCategory = category;
        //     }
        // }

        // if (!preferredCategory) return undefined;



        // const candidates = allLandmarks.filter(
        //     (landmark) => landmark.category === preferredCategory &&
        //         !visitedIds.has(landmark.id)
        // );

      
        // // 추천 후보가 없다면 undifined
        // if (candidates.length === 0) {
        //     return undefined;
        // }

        // // 후보중 1개 추천  
        // //return candidates[Math.floor(Math.random() * candidates.length)];
        // // 미방문 랜드마크 중 1개선택
        // const randomIndex = Math.floor(Math.random() * candidates.length);
        // //    const categoryOrder = 
        // return unvisited[randomIndex];
    }
}