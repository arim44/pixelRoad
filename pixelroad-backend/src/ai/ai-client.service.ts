// 실제 AI API를 호출
import { InferenceClient } from "@huggingface/inference";
import { Injectable, InternalServerErrorException } from "@nestjs/common";
import { AI_REPORT_SYSTEM_PROMPTS } from "./constants/ai-report-prompt.constant";

interface AiReportResult {
    analysis: string;
    reason: string;
}

@Injectable()
export class AiClientService {
    private readonly client: InferenceClient;

    constructor() {
        const token = process.env.HF_TOKEN?.trim();

        if (!token) {
            throw new Error("HF_TOKEN 이 설정되지 않았습니다.");
        }

        // Hugging Face Inference API 클라이언트를 초기화합니다.
        this.client = new InferenceClient(token);
    }

    /**
     * Hugging Face Chat Completion API를 호출하여
     * 탐험 리포트를 생성합니다.
     *
     * AI의 응답은 JSON 형태를 기대하지만,
     * 모델 특성상 코드블록이나 부가 설명이 포함될 수 있으므로
     * 응답을 추출 → 파싱 → 검증하는 단계를 거칩니다.
     */
    async generateReport(userPrompt: string): Promise<AiReportResult> {
        try {
            const response = await this.client.chatCompletion({
                model: process.env.HF_MODEL!,
                messages: [
                    {
                        role: "system",
                        content: AI_REPORT_SYSTEM_PROMPTS.AIREPORT_ANALYSIS,
                    },
                    {
                        role: "user",
                        content: userPrompt
                    },
                ],
                temperature: 0.7,
                max_tokens: 700,
            });

            //1 AI 응답확인
            const choice = response.choices[0];

            // AI 응답에 메시지가 없는 경우 정상적인 응답으로 처리하지 않습니다.
            if (!choice?.message) {
                throw new InternalServerErrorException(
                    "AI 응답 메시지가 없습니다."
                );
            }

            // 토큰 제한으로 응답이 중간에 끊긴 경우
            // 불완전한 JSON을 파싱하지 않도록 명시적으로 처리합니다.
            if (choice.finish_reason === "length") {
                throw new InternalServerErrorException(
                    "AI 응답이 토큰 제한으로 중단되었습니다."
                );
            }

            const content = choice.message.content;

            console.log("AI FINISH REASON:", choice.finish_reason);
            console.log("AI CONTENT:", content);

            // 모델이 빈 응답을 반환한 경우 처리합니다.
            if (typeof content !== "string" || !content.trim()) {
                throw new InternalServerErrorException(
                    "AI 응답이 비어있습니다.",
                );
            }

             // 모델 응답에서 실제 JSON 부분만 추출합니다.
            const jsonText = this.extractJson(content);

            // JSON 문자열을 JavaScript 객체로 변환합니다.
            const parsed = this.parseJson(jsonText);
           
            // 파싱된 객체가 애플리케이션에서 기대하는 구조인지 검증합니다.
            return this.validateResult(parsed);

        } catch (error) {
            console.error("AI 호출실패", error);
            // 이미 의도적으로 생성한 NestJS 예외는 그대로 전달합니다.
            if (error instanceof InternalServerErrorException) {
                throw error;
            }

            // 외부 API 오류 등 예상하지 못한 오류는
            // 서비스 내부 구현을 노출하지 않고 공통 오류로 변환합니다.
            throw new InternalServerErrorException(
                "AI 탐험 리포트 생성에 실패했습니다.",
            );
        }
    }

    
    /**
     * AI 응답에서 JSON 객체 부분을 추출합니다.
     *
     * 모델이 아래와 같이 응답할 수 있기 때문에
     * 순수 JSON만 반환된다는 것을 전제로 하지 않습니다.
     *
     * 1. 순수 JSON
     * 2. ```json 코드블록으로 감싼 JSON
     * 3. JSON 앞뒤에 설명이 포함된 응답
     */
    private extractJson(content: string): string {
        let text = content
            .trim()
            .replace(/^```json\s*/i, "")
            .replace(/^```\s*/i, "")
            .replace(/\s*```$/i, "")
            .trim();

        // 이미 JSON 객체 형태라면 추가적인 추출 없이 반환합니다.
        if (text.startsWith("{") && text.endsWith("}")) {
            return text;
        }

        // 응답에 부가 설명이 포함된 경우 첫 번째 {부터 마지막 }
        // 까지를 JSON 후보 영역으로 사용합니다.     
        const startIndex = text.indexOf("{");
        const endIndex = text.lastIndexOf("}");

        if (startIndex === -1 || endIndex === -1 || startIndex > endIndex) {
            throw new InternalServerErrorException(
                "AI 응답에서 JSON을 찾을 수 없습니다.",
            );
        }
      
        return text.slice(startIndex, endIndex + 1);
    }

    /**
     * JSON 문자열을 JavaScript 객체로 변환합니다.
     *
     * JSON 파싱 실패 시 원본 응답을 그대로 상위 로직에 전달하지 않고
     * 애플리케이션에서 처리할 수 있는 NestJS 예외로 변환합니다.
     */
    private parseJson(jsonText: string) : unknown {
        try{
            return JSON.parse(jsonText);
        } catch (error) {
            console.error("AI JSON 파싱 실패");
            console.error("추출된 JSON:", jsonText);

            throw new InternalServerErrorException(
                "AI 응답을 JSON으로 변환할 수 없습니다."
            );
        }
    }

    /**
     * AI가 반환한 객체가 탐험 리포트에서 요구하는 구조인지 검증합니다.
     *
     * TypeScript 타입은 런타임에서 보장되지 않기 때문에
     * 외부 API에서 받은 데이터는 별도로 검증해야 합니다.
     */
    private validateResult(data: unknown): AiReportResult {
        if (!data || typeof data !== "object") {
            throw new InternalServerErrorException(
                "AI 응답 형식이 올바르지 않습니다.",
            );
        }

        const result = data as Record<string, unknown>;

        if (
            typeof result.analysis !== "string" ||
            typeof result.reason !== "string"
        ) {
            console.error("잘못된 AI 응답:", result);

            throw new InternalServerErrorException(
                "AI 응답에 analysis 또는 reason이 없습니다.",
            );
        }

        return {
            analysis: result.analysis.trim(),
            reason: result.reason.trim(),
        };
    }
}