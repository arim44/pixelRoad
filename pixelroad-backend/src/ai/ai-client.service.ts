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

        // AI(휴깅페이스) 호출
        this.client = new InferenceClient(token);
    }

    // systemPrompt: string,
    async generateReport(userPrompt: string): Promise<AiReportResult> {

        // 실제 ai api 호출
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
                max_tokens: 1000,
            });

            // const content = response.choices[0]?.message?.content;

            //1 AI 응답확인
            const message = response.choices[0]?.message;

            if (!message) {
                throw new InternalServerErrorException(
                    "AI 응답 메시지가 없습니다."
                );
            }

            const finishReason = response.choices[0]?.finish_reason;

            console.log("===== AI RESPONSE =====");
            console.dir(response, { depth: null });
            console.log("======================");

            console.log("AI FINISH REASON:", finishReason);
            console.log("AI CONTENT:", message.content);
            console.log("AI REASONING:", message.reasoning);

            // 정상적인 경우 content 사용
            // content가 없고 reasoning에 JSON이 들어온 경우 fallback
            //const content = message.content;
            const content =
                typeof message.content === "string" &&
                    message.content.trim()
                    ? message.content
                    : typeof message.reasoning === "string" &&
                        message.reasoning.trim()
                        ? message.reasoning
                        : null;

            console.log("AI PARSE TARGET:", content);


             if (!content) {
            if (finishReason === "length") {
                throw new InternalServerErrorException(
                    "AI 응답이 토큰 제한으로 중단되었습니다."
                );
            }

            throw new InternalServerErrorException(
                "AI 응답이 비어있습니다."
            );
        }
            // 2 JSON 안전하게 추출
            const jsonText = this.extractJson(content);

            // 3 jSON 파싱
            let parsed: unknown;

            try {
                parsed = JSON.parse(jsonText);
            } catch (error) {
                console.error("AI JSON 파싱 실패");
                console.error("원본 content:", content);
                console.error("추출된 JSON:", jsonText);

                throw new InternalServerErrorException(
                    "AI 응답을 JSON으로 변환할 수 없습니다.",
                );
            }

            // 4 결과 형식 검증
            return this.validateResult(parsed);

            // const result = JSON.parse(content) as AiReportResult;
            // return result;

        } catch (error) {
            console.error("AI 호출실패", error);
            // 이미 만든 Nest 예외라면 그대로 전달
            if (error instanceof InternalServerErrorException) {
                throw error;
            }

            throw new InternalServerErrorException(
                "AI 탐험 리포트 생성에 실패했습니다.",
            );
        }  
    }

    /**
  * AI가 JSON 앞뒤에 설명을 붙이거나
  * ```json ... ``` 형태로 반환해도 JSON 부분만 추출
  */
    private extractJson(content: string): string {
        let text = content.trim();

        // -----------------------------
        // 코드블록 제거
        // ```json
        // {
        //   ...
        // }
        // ```
        // -----------------------------
        text = text
            .replace(/^```json\s*/i, "")
            .replace(/^```\s*/i, "")
            .replace(/\s*```$/i, "")
            .trim();

        // -----------------------------
        // 이미 JSON이면 바로 반환
        // -----------------------------
        if (text.startsWith("{") && text.endsWith("}")) {
            return text;
        }

        // -----------------------------
        // JSON 앞뒤에 이상한 문장이 있는 경우
        //
        // 예:
        // "분석 결과입니다.
        // {
        //   "analysis": "...",
        //   "reason": "..."
        // }
        // 감사합니다."
        // -----------------------------
        const startIndex = text.indexOf("{");
        const endIndex = text.lastIndexOf("}");

        if (startIndex === -1 || endIndex === -1) {
            throw new InternalServerErrorException(
                "AI 응답에서 JSON을 찾을 수 없습니다.",
            );
        }

        if (startIndex > endIndex) {
            throw new InternalServerErrorException(
                "AI 응답의 JSON 형식이 올바르지 않습니다.",
            );
        }

        return text.slice(startIndex, endIndex + 1);
    }

    /**
     * AI가 반환한 JSON 구조를 검증
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