// 실제 AI API를 호출
import { InferenceClient } from "@huggingface/inference";
import { Injectable, InternalServerErrorException } from "@nestjs/common";
import { response } from "express";
import { AI_REPORT_SYSTEM_PROMPTS } from "./constants/ai-report-prompt.constant";

interface AiReportResult{
    analysis: string;
    reason: string;
}

@Injectable()
export class AiClientService {
    private readonly client: InferenceClient;
    constructor() {
        const token = process.env.HF_TOKEN?.trim();

        // console.log("HF_TOKEN 존재:", !!token);
        // console.log(
        //     "HF_TOKEN 앞 5자리:",
        //     token?.slice(0, 5)
        // );
        // console.log(
        //     "HF_TOKEN 뒤 5자리:",
        //     token?.slice(-5)
        // );

        // if(!token)  throw new Error("HF_TOKEN이 설정되지 않았습니다.");

        // 젬마(휴깅페이스) 호출
        this.client = new InferenceClient(token);
    }

    async generateReport(        // systemPrompt: string,
        userPrompt: string): Promise<AiReportResult>{
        //   console.log("AI에게 전달할 Prompt:");
        // console.log(prompt);

        // Todo: 실제 ai api 호출
        try{
            const response = await this.client.chatCompletion({
                model: process.env.HF_MODEL!,
                messages: [
                    {
                       role: "system",
                       content: AI_REPORT_SYSTEM_PROMPTS.AIREPORT_ANALYSIS, 
                    },
                    {
                        role : "user",
                        content: userPrompt
                    },
                ],
                temperature: 0.7,
                max_tokens: 300,
            });

            const content = response.choices[0]?.message?.content;

            if(!content) {
                throw new InternalServerErrorException(
                    "AI 응답이 비어있습니다."
                );
            }

            const result = JSON.parse(content) as AiReportResult;
            return result;

        } catch(error) {
            console.error("Gemma 호출실패", error);

            throw error;
        }
        // return{
        //     analysis: "역사와 문화 관련 랜드마크를 중심으로 탐험하는 성향입니다.",
        //     reason: "역사 카테고리의 랜드마크를 자주 방문하고 있어 창경궁을 통해 조선 시대 문화 탐험을 확장할 수 있습니다.",
        // };       
    }
}